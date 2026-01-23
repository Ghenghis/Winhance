# Winhance-FS Enhancement Roadmap 🗺️

This document outlines the implementation roadmap for advanced file management features in Winhance-FS.

---

## Project Vision

Winhance-FS combines the power of the original Winhance Windows optimization tool with an AI-powered file management system (originally codenamed "NexusFS"). The goal is to create a comprehensive Windows 11 native application that:

- **Outperforms Everything Search** using MFT/USN Journal + SIMD
- **Provides AI-powered file organization** with semantic search
- **Integrates with AI tools** via MCP (Claude Code, Windsurf, LM Studio)
- **Manages storage intelligently** with space recovery and model relocation
- **Offers professional file management** with dual-pane browser, batch rename, and more

---

## Implementation Phases

### Phase 1: Foundation ✅
*Status: Completed*

| Component            | Description                     | Status |
| -------------------- | ------------------------------- | ------ |
| 3-Tier Architecture  | WPF + C# Service + Rust Backend | ✅ Done |
| UniFFI Integration   | Rust-C# interop bindings        | ✅ Done |
| Borg Theme Studio    | 5-color theming system          | ✅ Done |
| Storage Intelligence | Drive analysis, space recovery  | ✅ Done |
| Deep Scan            | MFT-based file indexing         | ✅ Done |
| Transaction System   | Rollback support for operations | ✅ Done |
| MCP Server           | Basic AI tool integration       | ✅ Done |
| Python Agents        | File discovery, classification  | ✅ Done |

### Phase 2: Advanced File Manager 🔄
*Status: In Progress*

| Component             | Description                 | Status    | Priority |
| --------------------- | --------------------------- | --------- | -------- |
| Dual-Pane Browser     | Side-by-side directory view | 🔄 Design  | High     |
| Tabbed Interface      | Multiple locations in tabs  | 🔄 Design  | High     |
| Quick Access Panel    | Favorites, drives, recent   | 🔄 Design  | High     |
| Breadcrumb Navigation | Click-to-navigate paths     | 🔄 Design  | Medium   |
| Column Customization  | Show/hide file attributes   | 📋 Planned | Medium   |
| Preview Pane          | Quick file preview          | 📋 Planned | Low      |

### Phase 3: Batch Rename System 📋
*Status: Planned*

| Component           | Description                 | Status    | Priority |
| ------------------- | --------------------------- | --------- | -------- |
| Rename Rules Engine | Find/replace, counter, case | 📋 Planned | High     |
| Live Preview        | Real-time rename preview    | 📋 Planned | High     |
| Regex Support       | Advanced pattern matching   | 📋 Planned | High     |
| Metadata Extraction | EXIF, ID3 tags in names     | 📋 Planned | Medium   |
| Presets System      | Save/load rule combinations | 📋 Planned | Medium   |
| Context Menu        | Right-click batch rename    | 📋 Planned | High     |
| CLI Integration     | Command-line renaming       | 📋 Planned | Medium   |

### Phase 4: Smart Organizer 📋
*Status: Planned*

| Component               | Description                  | Status    | Priority |
| ----------------------- | ---------------------------- | --------- | -------- |
| Type-Based Organization | Organize by file extension   | 📋 Planned | High     |
| Date-Based Organization | Organize by date             | 📋 Planned | High     |
| AI Classification       | Semantic categorization      | 📋 Planned | Medium   |
| Custom Rules Engine     | User-defined organization    | 📋 Planned | Medium   |
| Watch Folders           | Auto-organize on changes     | 📋 Planned | Medium   |
| Scheduled Tasks         | Recurring organization       | 📋 Planned | Low      |
| Duplicate Detection     | Hash and perceptual matching | 📋 Planned | High     |

### Phase 5: Space Management 📋
*Status: Planned*

| Component         | Description                  | Status    | Priority |
| ----------------- | ---------------------------- | --------- | -------- |
| Model Relocator   | Move AI models with symlinks | 📋 Planned | Critical |
| Cache Cleaner     | Clean dev caches safely      | 📋 Planned | High     |
| Duplicate Remover | Remove duplicate files       | 📋 Planned | High     |
| Temp File Cleaner | Safe temp cleanup            | 📋 Planned | Medium   |
| Archive Manager   | Old files to archive drive   | 📋 Planned | Medium   |

### Phase 6: Enhanced Search 📋
*Status: Planned*

| Component          | Description              | Status    | Priority |
| ------------------ | ------------------------ | --------- | -------- |
| SIMD String Search | memchr-based fast search | 📋 Planned | High     |
| Bloom Filter       | Fast negative lookups    | 📋 Planned | High     |
| Size/Date Filters  | Advanced filter syntax   | 📋 Planned | High     |
| Content Search     | Search inside files      | 📋 Planned | Medium   |
| Regex Search       | Pattern matching         | 📋 Planned | Medium   |
| Semantic Search    | AI-powered by meaning    | 📋 Planned | Medium   |
| Saved Searches     | Store frequent queries   | 📋 Planned | Low      |

### Phase 7: Windows Integration 📋
*Status: Planned*

| Component          | Description             | Status    | Priority |
| ------------------ | ----------------------- | --------- | -------- |
| Shell Context Menu | Explorer right-click    | 📋 Planned | High     |
| Quick Access       | Pin to Explorer sidebar | 📋 Planned | Medium   |
| Thumbnail Provider | Custom file previews    | 📋 Planned | Low      |
| Jump Lists         | Taskbar recent items    | 📋 Planned | Low      |
| Notifications      | Toast notifications     | 📋 Planned | Medium   |

---

## Technical Architecture

### Component Stack

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         WINHANCE-FS ARCHITECTURE                             │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌─ PRESENTATION LAYER (Winhance.WPF) ──────────────────────────────────┐  │
│  │                                                                       │  │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │  │
│  │  │ File Manager│  │ Batch Rename│  │  Organizer  │  │   Storage   │  │  │
│  │  │  Dashboard  │  │  Dashboard  │  │  Dashboard  │  │   Intel     │  │  │
│  │  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘  │  │
│  │                                                                       │  │
│  │  ┌─────────────────────────────────────────────────────────────────┐  │  │
│  │  │                    Borg Theme Studio                             │  │  │
│  │  └─────────────────────────────────────────────────────────────────┘  │  │
│  │                                                                       │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
│                                      │                                       │
│                                      ▼                                       │
│  ┌─ SERVICE LAYER (Winhance.Infrastructure) ────────────────────────────┐  │
│  │                                                                       │  │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │  │
│  │  │ FileManager │  │ BatchRename │  │  Organizer  │  │  Transaction│  │  │
│  │  │   Service   │  │   Service   │  │   Service   │  │   Manager   │  │  │
│  │  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘  │  │
│  │                                                                       │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
│                                      │                                       │
│                                      ▼                                       │
│  ┌─ DOMAIN LAYER (Winhance.Core) ───────────────────────────────────────┐  │
│  │                                                                       │  │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │  │
│  │  │IFileManager │  │IBatchRename │  │ IOrganizer  │  │ITransaction │  │  │
│  │  │   Service   │  │   Service   │  │   Service   │  │   Service   │  │  │
│  │  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘  │  │
│  │                                                                       │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
│                                      │                                       │
│                                      ▼                                       │
│  ┌─ NATIVE LAYER (nexus-native - Rust) ─────────────────────────────────┐  │
│  │                                                                       │  │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │  │
│  │  │ MFT Parser  │  │SIMD Search  │  │Bloom Filter │  │  Tantivy    │  │  │
│  │  │(ntfs crate) │  │  (memchr)   │  │ (fastbloom) │  │  (search)   │  │  │
│  │  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘  │  │
│  │                                                                       │  │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │  │
│  │  │ USN Journal │  │  xxHash     │  │  SHA256     │  │  Windows    │  │  │
│  │  │  (monitor)  │  │  (fast)     │  │  (verify)   │  │   APIs      │  │  │
│  │  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘  │  │
│  │                                                                       │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
│                                      │                                       │
│                                      ▼                                       │
│  ┌─ AI LAYER (nexus-agents - Python) ───────────────────────────────────┐  │
│  │                                                                       │  │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │  │
│  │  │   MCP       │  │  Embeddings │  │  Vector DB  │  │    OCR      │  │  │
│  │  │   Server    │  │(transformers│  │  (Qdrant)   │  │  (Surya)    │  │  │
│  │  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘  │  │
│  │                                                                       │  │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │  │
│  │  │ File Agent  │  │Classification│  │ Organization│  │  Cleanup    │  │  │
│  │  │ (discovery) │  │   Agent     │  │    Agent    │  │   Agent     │  │  │
│  │  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘  │  │
│  │                                                                       │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Performance Targets

| Metric                | Target   | Technology          |
| --------------------- | -------- | ------------------- |
| Index 1M files        | < 1 sec  | MFT direct access   |
| Search latency        | < 5 ms   | SIMD + Bloom filter |
| Memory per 1M files   | < 30 MB  | Compressed entries  |
| Batch rename preview  | < 100 ms | Parallel processing |
| Organization analysis | < 2 sec  | Multi-threaded scan |

---

## Feature Integration Points

### File Manager Dashboard

```csharp
// FeatureIds.cs - New features to add
public const string FileManager = "FileManager";
public const string FileManagerBrowser = "FileManager.Browser";
public const string FileManagerBatchRename = "FileManager.BatchRename";
public const string FileManagerOrganizer = "FileManager.Organizer";
public const string FileManagerSearch = "FileManager.Search";

// FeatureDefinitions.cs - Dashboard registration
new(FeatureIds.FileManager, "File Manager", "FolderMultiple", "Files", 1),
new(FeatureIds.FileManagerBatchRename, "Batch Rename", "RenameBox", "Files", 2),
new(FeatureIds.FileManagerOrganizer, "Smart Organizer", "FolderStar", "Files", 3),
new(FeatureIds.FileManagerSearch, "Advanced Search", "Search", "Files", 4),
```

### MCP Tools Extension

```python
# New MCP tools to implement
@mcp.tool()
async def fm_browse(path: str) -> dict: ...
@mcp.tool()
async def fm_batch_rename(files: list, rules: list) -> dict: ...
@mcp.tool()
async def fm_organize(source: str, strategy: str) -> dict: ...
@mcp.tool()
async def fm_search(query: str, options: dict) -> list: ...
@mcp.tool()
async def fm_space_recovery(drive: str) -> dict: ...
@mcp.tool()
async def fm_relocate_models(source: str, dest: str) -> dict: ...
```

### CLI Commands Extension

```bash
# New CLI commands
winhance-fs fm browse [path]
winhance-fs fm rename --pattern "*.jpg" --rules "..."
winhance-fs fm organize --source Downloads --strategy type
winhance-fs fm search "query" --filters "..."
winhance-fs fm space C:\ --recover
```

---

## Dependencies

### New Rust Crates

```toml
# Cargo.toml additions
[dependencies]
memchr = "2.7"          # SIMD string search
fastbloom = "0.7"       # Bloom filter
rayon = "1.10"          # Parallel processing
regex = "1.10"          # Regex for batch rename
walkdir = "2.5"         # Directory traversal
xxhash-rust = "0.8"     # Fast hashing
sha2 = "0.10"           # Integrity verification
```

### New Python Packages

```toml
# pyproject.toml additions
dependencies = [
    "qdrant-client>=1.7",
    "sentence-transformers>=2.2",
    "chromadb>=0.4",
    "watchdog>=4.0",
    "aiofiles>=23.0",
]
```

### New NuGet Packages

```xml
<!-- .csproj additions -->
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
<PackageReference Include="Microsoft.WindowsAPICodePack" Version="1.1.5" />
```

---

## Timeline Estimates

| Phase                        | Duration  | Dependencies      |
| ---------------------------- | --------- | ----------------- |
| Phase 2: File Manager        | 4-6 weeks | Phase 1           |
| Phase 3: Batch Rename        | 2-3 weeks | Phase 2           |
| Phase 4: Organizer           | 3-4 weeks | Phase 2, AI Layer |
| Phase 5: Space Management    | 2-3 weeks | Phase 4           |
| Phase 6: Enhanced Search     | 3-4 weeks | Rust Backend      |
| Phase 7: Windows Integration | 2-3 weeks | All phases        |

**Total Estimated Duration: 16-23 weeks**

---

## Open Source Integration

| Project                                                     | Purpose           | Integration Point       |
| ----------------------------------------------------------- | ----------------- | ----------------------- |
| [fd](https://github.com/sharkdp/fd)                         | Fast file finding | Search engine reference |
| [Tantivy](https://github.com/quickwit-oss/tantivy)          | Full-text search  | Already integrated      |
| [AIFS](https://github.com/OpenInterpreter/aifs)             | Semantic search   | AI classification       |
| [LlamaFS](https://github.com/iyaja/llama-fs)                | AI organization   | Organization patterns   |
| [Qdrant](https://github.com/qdrant/qdrant)                  | Vector database   | Embedding storage       |
| [Bulk Rename Utility](https://www.bulkrenameutility.co.uk/) | Rename features   | Feature reference       |

---

## Success Metrics

### Performance

- [ ] Index 1M files in < 1 second
- [ ] Search returns results in < 5ms
- [ ] Batch rename preview updates in < 100ms
- [ ] Organization analysis completes in < 2 seconds

### Functionality

- [ ] All file manager features working
- [ ] Batch rename with 10+ rule types
- [ ] Smart organizer with 5+ strategies
- [ ] Context menu integration working
- [ ] MCP tools fully functional

### User Experience

- [ ] Silk-smooth operation (60 FPS)
- [ ] Never laggy or unresponsive
- [ ] Intuitive UI matching Winhance style
- [ ] Complete undo/rollback support
- [ ] Comprehensive documentation

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines on contributing to these features.

### Priority Contributions Needed

1. **Rust Backend**: SIMD search optimization
2. **WPF UI**: File manager components
3. **Python AI**: Classification models
4. **Testing**: Unit and integration tests
5. **Documentation**: User guides

---

*Last updated: January 18, 2026*

*See also: [ARCHITECTURE.md](ARCHITECTURE.md) | [DEVELOPMENT.md](DEVELOPMENT.md) | [FEATURES.md](FEATURES.md)*
