//! Tantivy-based search engine
//!
//! Provides full-text search with fuzzy matching, filters, and ranking.

use crate::{FileEntry, NexusError, Result};
use std::path::Path;
use std::sync::Arc;
use tantivy::{
    collector::TopDocs,
    directory::MmapDirectory,
    doc,
    query::{FuzzyTermQuery, QueryParser, TermQuery},
    schema::{Field, Schema, Value, FAST, STORED, TEXT},
    Index, IndexReader, IndexWriter, ReloadPolicy, Term,
};
use tracing::{debug, info};

/// Search result with score
#[derive(Debug, Clone)]
pub struct SearchResult {
    pub entry: FileEntry,
    pub score: f32,
    pub snippet: Option<String>,
}

/// Search query options
#[derive(Debug, Clone)]
pub struct SearchQuery {
    /// Search query string
    pub query: String,
    /// Search type
    pub search_type: SearchType,
    /// Maximum results
    pub limit: usize,
    /// File type filter
    pub file_types: Option<Vec<String>>,
    /// Minimum file size
    pub min_size: Option<u64>,
    /// Maximum file size
    pub max_size: Option<u64>,
    /// Drive filter
    pub drives: Option<Vec<char>>,
    /// Only directories
    pub dirs_only: bool,
    /// Only files
    pub files_only: bool,
}

#[derive(Debug, Clone, PartialEq)]
pub enum SearchType {
    /// Full-text semantic search
    Semantic,
    /// Glob pattern matching
    Glob,
    /// Regex pattern matching
    Regex,
    /// Exact match
    Exact,
    /// Fuzzy matching
    Fuzzy,
}

impl Default for SearchQuery {
    fn default() -> Self {
        Self {
            query: String::new(),
            search_type: SearchType::Semantic,
            limit: 100,
            file_types: None,
            min_size: None,
            max_size: None,
            drives: None,
            dirs_only: false,
            files_only: false,
        }
    }
}

/// Tantivy search engine
pub struct SearchEngine {
    index: Index,
    reader: IndexReader,
    writer: Arc<parking_lot::Mutex<IndexWriter>>,
    #[allow(dead_code)]
    schema: Schema,
    // Field references
    field_path: Field,
    field_name: Field,
    field_extension: Field,
    field_size: Field,
    field_is_dir: Field,
    field_drive: Field,
    field_parent: Field,
    field_modified: Field,
}

impl SearchEngine {
    /// Create or open a search engine at the given path
    pub fn new<P: AsRef<Path>>(index_path: P) -> Result<Self> {
        let index_path = index_path.as_ref();

        // Create schema
        let mut schema_builder = Schema::builder();

        let field_path = schema_builder.add_text_field("path", TEXT | STORED);
        let field_name = schema_builder.add_text_field("name", TEXT | STORED);
        let field_extension = schema_builder.add_text_field("extension", TEXT | STORED);
        let field_size = schema_builder.add_u64_field("size", FAST | STORED);
        let field_is_dir = schema_builder.add_u64_field("is_dir", FAST | STORED);
        let field_drive = schema_builder.add_text_field("drive", TEXT | STORED);
        let field_parent = schema_builder.add_text_field("parent", TEXT | STORED);
        let field_modified = schema_builder.add_i64_field("modified", FAST | STORED);

        let schema = schema_builder.build();

        // Create or open index
        let index = if index_path.exists() {
            Index::open_in_dir(index_path)
                .map_err(|e| NexusError::Index(format!("Failed to open index: {}", e)))?
        } else {
            std::fs::create_dir_all(index_path)?;
            let dir = MmapDirectory::open(index_path).map_err(|e| {
                NexusError::Index(format!("Failed to create index directory: {}", e))
            })?;
            Index::create(dir, schema.clone(), tantivy::IndexSettings::default())
                .map_err(|e| NexusError::Index(format!("Failed to create index: {}", e)))?
        };

        // Create reader with auto-reload
        let reader = index
            .reader_builder()
            .reload_policy(ReloadPolicy::OnCommitWithDelay)
            .try_into()
            .map_err(|e| NexusError::Index(format!("Failed to create reader: {}", e)))?;

        // Create writer with 50MB buffer
        let writer = index
            .writer(50_000_000)
            .map_err(|e| NexusError::Index(format!("Failed to create writer: {}", e)))?;

        info!("Search engine initialized at {:?}", index_path);

        Ok(Self {
            index,
            reader,
            writer: Arc::new(parking_lot::Mutex::new(writer)),
            schema,
            field_path,
            field_name,
            field_extension,
            field_size,
            field_is_dir,
            field_drive,
            field_parent,
            field_modified,
        })
    }

    /// Index a batch of file entries
    pub fn index_entries(&self, entries: &[FileEntry]) -> Result<()> {
        let mut writer = self.writer.lock();

        for entry in entries {
            let modified_ts = entry.modified.map(|dt| dt.timestamp()).unwrap_or(0);

            writer
                .add_document(doc!(
                    self.field_path => entry.path.clone(),
                    self.field_name => entry.name.clone(),
                    self.field_extension => entry.extension.clone().unwrap_or_default(),
                    self.field_size => entry.size,
                    self.field_is_dir => if entry.is_dir { 1u64 } else { 0u64 },
                    self.field_drive => entry.drive.to_string(),
                    self.field_parent => entry.parent.clone(),
                    self.field_modified => modified_ts,
                ))
                .map_err(|e| NexusError::Index(format!("Failed to add document: {}", e)))?;
        }

        writer
            .commit()
            .map_err(|e| NexusError::Index(format!("Failed to commit: {}", e)))?;

        info!("Indexed {} entries", entries.len());
        Ok(())
    }

    /// Search for files
    pub fn search(&self, query: &SearchQuery) -> Result<Vec<SearchResult>> {
        let searcher = self.reader.searcher();

        let tantivy_query: Box<dyn tantivy::query::Query> = match query.search_type {
            SearchType::Exact => Box::new(TermQuery::new(
                Term::from_field_text(self.field_name, &query.query),
                tantivy::schema::IndexRecordOption::Basic,
            )),
            SearchType::Fuzzy => {
                Box::new(FuzzyTermQuery::new(
                    Term::from_field_text(self.field_name, &query.query),
                    2, // Edit distance
                    true,
                ))
            }
            SearchType::Glob | SearchType::Regex => {
                // Use regex query for glob patterns
                let pattern = if query.search_type == SearchType::Glob {
                    glob_to_regex(&query.query)
                } else {
                    query.query.clone()
                };

                let query_parser =
                    QueryParser::for_index(&self.index, vec![self.field_name, self.field_path]);
                query_parser
                    .parse_query(&pattern)
                    .map_err(|e| NexusError::Search(format!("Invalid query: {}", e)))?
            }
            SearchType::Semantic => {
                // Full-text search across name and path
                let query_parser =
                    QueryParser::for_index(&self.index, vec![self.field_name, self.field_path]);
                query_parser
                    .parse_query(&query.query)
                    .map_err(|e| NexusError::Search(format!("Invalid query: {}", e)))?
            }
        };

        let top_docs = searcher
            .search(&tantivy_query, &TopDocs::with_limit(query.limit))
            .map_err(|e| NexusError::Search(format!("Search failed: {}", e)))?;

        let mut results = Vec::new();

        for (score, doc_address) in top_docs {
            let doc: tantivy::TantivyDocument = searcher
                .doc(doc_address)
                .map_err(|e| NexusError::Search(format!("Failed to retrieve doc: {}", e)))?;

            let path = doc
                .get_first(self.field_path)
                .and_then(|v| v.as_str())
                .unwrap_or_default()
                .to_string();

            let name = doc
                .get_first(self.field_name)
                .and_then(|v| v.as_str())
                .unwrap_or_default()
                .to_string();

            let extension = doc
                .get_first(self.field_extension)
                .and_then(|v| v.as_str())
                .map(|s| s.to_string())
                .filter(|s| !s.is_empty());

            let size = doc
                .get_first(self.field_size)
                .and_then(|v| v.as_u64())
                .unwrap_or(0);

            let is_dir = doc
                .get_first(self.field_is_dir)
                .and_then(|v| v.as_u64())
                .map(|v| v == 1)
                .unwrap_or(false);

            let drive = doc
                .get_first(self.field_drive)
                .and_then(|v| v.as_str())
                .and_then(|s| s.chars().next())
                .unwrap_or('C');

            let parent = doc
                .get_first(self.field_parent)
                .and_then(|v| v.as_str())
                .unwrap_or_default()
                .to_string();

            // Apply filters
            if query.files_only && is_dir {
                continue;
            }
            if query.dirs_only && !is_dir {
                continue;
            }
            if let Some(min) = query.min_size {
                if size < min {
                    continue;
                }
            }
            if let Some(max) = query.max_size {
                if size > max {
                    continue;
                }
            }
            if let Some(ref types) = query.file_types {
                if let Some(ext) = &extension {
                    if !types.iter().any(|t| t.eq_ignore_ascii_case(ext)) {
                        continue;
                    }
                } else {
                    continue;
                }
            }
            if let Some(ref drives) = query.drives {
                if !drives.contains(&drive) {
                    continue;
                }
            }

            let entry = FileEntry {
                path,
                name,
                extension,
                size,
                created: None,
                modified: None,
                accessed: None,
                is_dir,
                is_hidden: false,
                is_system: false,
                content_hash: None,
                parent,
                drive,
            };

            results.push(SearchResult {
                entry,
                score,
                snippet: None,
            });
        }

        debug!(
            "Search '{}' returned {} results",
            query.query,
            results.len()
        );
        Ok(results)
    }

    /// Clear the entire index
    pub fn clear(&self) -> Result<()> {
        let mut writer = self.writer.lock();
        writer
            .delete_all_documents()
            .map_err(|e| NexusError::Index(format!("Failed to clear index: {}", e)))?;
        writer
            .commit()
            .map_err(|e| NexusError::Index(format!("Failed to commit: {}", e)))?;
        Ok(())
    }

    /// Get index statistics
    pub fn stats(&self) -> (u64, u64) {
        let searcher = self.reader.searcher();
        let num_docs = searcher.num_docs();
        let num_segments = searcher.segment_readers().len() as u64;
        (num_docs, num_segments)
    }
}

/// Convert glob pattern to regex
fn glob_to_regex(glob: &str) -> String {
    let mut regex = String::new();
    regex.push('^');

    for c in glob.chars() {
        match c {
            '*' => regex.push_str(".*"),
            '?' => regex.push('.'),
            '.' | '+' | '(' | ')' | '[' | ']' | '{' | '}' | '^' | '$' | '|' | '\\' => {
                regex.push('\\');
                regex.push(c);
            }
            _ => regex.push(c),
        }
    }

    regex.push('$');
    regex
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_glob_to_regex() {
        assert_eq!(glob_to_regex("*.txt"), "^.*\\.txt$");
        assert_eq!(glob_to_regex("file?.txt"), "^file.\\.txt$");
        assert_eq!(glob_to_regex("test"), "^test$");
    }
}
