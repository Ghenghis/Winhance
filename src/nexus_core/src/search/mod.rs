//! Search module using Tantivy
//!
//! Provides ultra-fast full-text search using the Tantivy search engine.

mod tantivy_engine;

pub use tantivy_engine::{SearchEngine, SearchQuery, SearchResult};
