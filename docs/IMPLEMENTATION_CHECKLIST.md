# Winhance-FS Implementation Checklist

**Interactive tracking document - Check off items as completed**

**Last Updated:** January 22, 2026
**Status:** Updated with Fagan Audit Findings

---

## CRITICAL: Code Quality Remediation (From Fagan Audit)

> **PRIORITY:** These items must be completed BEFORE new feature development.
> See: [FAGAN_AUDIT_REPORT.md](FAGAN_AUDIT_REPORT.md) | [REMEDIATION_ACTION_PLAN.md](REMEDIATION_ACTION_PLAN.md)

### CQ-1: Error Handling Fixes (50+ issues)

- [x] **OutputParser.cs** - Replace 7 bare catch blocks with proper logging ✅ (Jan 22, 2026)
- [x] **WindowsRegistryService.cs** - Add logging to 12 exception handlers ✅ (Jan 22, 2026)
- [x] **WimUtilService.cs** - Fix bare catch blocks with Debug.WriteLine ✅ (Jan 22, 2026)
- [x] **LogService.cs** - Add fallback Debug.WriteLine for logging failures ✅ (Jan 22, 2026)
- [x] **BatchRenameService.cs** - Fix 5 bare catch blocks ✅ (Jan 22, 2026)
- [x] **DuplicateFinderService.cs** - Fix 3 bare catch blocks ✅ (Jan 22, 2026)
- [x] **FileManagerService.cs** - Fix 6 bare catch blocks ✅ (Jan 22, 2026)
- [x] **OrganizerService.cs** - Fix 9 bare catch blocks ✅ (Jan 22, 2026)
- [x] **PowerShellExecutionService.cs** - Fix cleanup bare catch ✅ (Jan 22, 2026)
- [x] **CompatibleSettingsRegistry.cs** - Fix reflection bare catches ✅ (Jan 22, 2026)
- [x] **App.xaml.cs** - Fix startup logging bare catch ✅ (Jan 22, 2026)
- [ ] **AutounattendScriptBuilder.cs** - Bare catches are in PowerShell script strings (not C# code)

### CQ-2: Async Anti-Pattern Fixes (6 issues)

- [x] **TooltipRefreshEventHandler.cs** - Already has try-catch (acceptable pattern)
- [x] **BaseSettingsFeatureViewModel.cs** - Fix async void OnLanguageChanged ✅ (Jan 22, 2026)
- [x] **App.xaml.cs** - Exception handlers already present ✅
- [x] **SoftwareAppsViewModel.cs** - Remove manual GC.Collect() calls ✅ (Jan 22, 2026)

### CQ-3: Memory Leak Fixes (8 issues)

- [x] **MainViewModel.cs** - Implement IDisposable, unsubscribe events ✅ (Jan 22, 2026)
- [x] **SoftwareAppsViewModel.cs** - Already has Dispose with event cleanup ✅
- [x] **WindowsAppsViewModel.cs** - Already has Dispose with event cleanup ✅

### CQ-4: Security Hardening (6 critical + 12 high)

- [ ] **WimUtilService.cs** - Replace PowerShell string interpolation with parameter binding
- [ ] **DriverCategorizer.cs** - Add path validation and symlink detection
- [ ] **DuplicateFinderService.cs** - Add input validation for paths and options
- [ ] **FileManagerService.cs** - Validate transaction log paths
- [ ] **WindowsRegistryService.cs** - Validate registry key paths

### CQ-5: Thread Safety Fixes (4 issues)

- [x] **SoftwareAppsViewModel.cs** - Use Interlocked.CompareExchange for thread safety ✅ (Jan 22, 2026)
- [x] **WindowsAppsViewModel.cs** - Cache invalidation uses proper flag pattern ✅

### CQ-6: DI/Architecture Fixes (4 issues)

- [ ] **LogService.cs** - Move initialization to constructor (remove Initialize method)
- [x] **DuplicateFinderService.cs** - Fixed optional _nexusIndexer dependency ✅ (Jan 22, 2026)
- [x] **AdvancedFileOperationsService.cs** - Fixed optional _nexusIndexer dependency ✅ (Jan 22, 2026)
- [x] **FileManagerViewModel.cs** - Made INexusIndexerService required ✅ (Jan 22, 2026)

### CQ-7: Accessibility Improvements

- [ ] **MainWindow.xaml** - Add AutomationProperties to all buttons
- [ ] Add loading indicators for async operations
- [ ] Ensure keyboard navigation works throughout UI

### CQ-8: C# Testing Infrastructure (0% coverage)

- [ ] Create `tests/Winhance.Core.Tests/` project
- [ ] Create `tests/Winhance.Infrastructure.Tests/` project
- [ ] Create `tests/Winhance.WPF.Tests/` project
- [ ] Add OutputParser unit tests
- [ ] Add WindowsRegistryService unit tests (with mocks)
- [ ] Add ViewModel unit tests
- [ ] Achieve minimum 50% code coverage on critical paths

---

## Phase 1: Critical Foundation

### 1.1 MFT Direct Reader (Rust)
- [ ] Create `mft_reader.rs` with Windows volume access
- [ ] Implement `CreateFileW` for `\\.\C:` volume handle
- [ ] Add `FSCTL_GET_NTFS_VOLUME_DATA` ioctl call
- [ ] Parse MFT record headers (FILE signature, sequence, link count)
- [ ] Extract Standard Information attribute ($SI)
- [ ] Extract File Name attribute ($FN) - both short and long names
- [ ] Extract Data attribute ($DATA) - file size
- [ ] Handle directory entries (index root/allocation)
- [ ] Build parent-child path mapping from MFT references
- [ ] Add error handling for access denied (non-admin)
- [ ] Implement MFT scan progress callback
- [ ] Add tests for MFT parsing

### 1.2 Search Integration
- [ ] Wire `FastIndexer` to `SearchEngine` in Rust
- [ ] Export `search()` function via UniFFI
- [ ] Implement Python wrapper for Rust search
- [ ] Update `nexus_mcp/server.py` `handle_search()` to call real search
- [ ] Update `nexus_cli/main.py` search command to use real search
- [ ] Add search result pagination
- [ ] Implement size filter (min/max)
- [ ] Implement extension filter
- [ ] Implement date range filter
- [ ] Add fuzzy search tolerance setting
- [ ] Add search history/recent searches

### 1.3 WPF Storage Module
- [ ] Add `StorageIntelligence` to `FeatureIds.cs`
- [ ] Add `DeepScan` to `FeatureIds.cs`
- [ ] Add `ModelManager` to `FeatureIds.cs`
- [ ] Add `CacheManager` to `FeatureIds.cs`
- [ ] Add `ForensicsTools` to `FeatureIds.cs`
- [ ] Add `FileManager` to `FeatureIds.cs`
- [ ] Add `BatchRename` to `FeatureIds.cs`
- [ ] Add `SmartOrganizer` to `FeatureIds.cs`
- [ ] Create `FeatureDefinitions` entries for Storage module
- [ ] Create `Features/Storage/` directory structure
- [ ] Create `IStorageService.cs` interface
- [ ] Create `StorageService.cs` implementation
- [ ] Create `StorageIntelligenceViewModel.cs`
- [ ] Create `StorageIntelligenceView.xaml`
- [ ] Register in DI container

---

## Phase 2: Core Features

### 2.1 USN Journal Monitor (Rust)
- [ ] Create `usn_journal.rs` module
- [ ] Implement `FSCTL_QUERY_USN_JOURNAL` to get journal state
- [ ] Implement `FSCTL_READ_USN_JOURNAL` to read records
- [ ] Parse `USN_RECORD_V2` and `USN_RECORD_V3` structures
- [ ] Detect file creates (`USN_REASON_FILE_CREATE`)
- [ ] Detect file deletes (`USN_REASON_FILE_DELETE`)
- [ ] Detect file renames (`USN_REASON_RENAME_NEW_NAME`)
- [ ] Detect file modifications (`USN_REASON_DATA_EXTEND`, `DATA_TRUNCATION`)
- [ ] Implement continuous monitoring thread
- [ ] Add callback for index updates
- [ ] Handle journal wrap-around
- [ ] Add graceful shutdown

### 2.2 Content Hashing (Rust)
- [ ] Create `content_hasher.rs` module
- [ ] Add xxhash-rust integration for fast hashing
- [ ] Add sha2 integration for verification hashes
- [ ] Implement partial file hash (first 64KB + file size)
- [ ] Implement full file hash for small files
- [ ] Add async/parallel hashing with rayon
- [ ] Create hash cache to avoid re-hashing
- [ ] Export hash functions via UniFFI

### 2.3 Symlink Management
- [ ] Create symlink utility in Rust
- [ ] Implement `CreateSymbolicLinkW` via windows-rs
- [ ] Handle admin elevation requirement
- [ ] Verify symlink creation success
- [ ] Add symlink detection (is this a symlink?)
- [ ] Add symlink resolution (where does it point?)
- [ ] Integrate with model relocator
- [ ] Add rollback support for symlink operations

### 2.4 Duplicate Detection
- [ ] Enhance `space_analyzer.py` hash computation
- [ ] Group files by size first (optimization)
- [ ] Compute quick hash for same-size files
- [ ] Compute full hash only for quick-hash matches
- [ ] Build duplicate groups data structure
- [ ] Add perceptual hashing for images (imagehash)
- [ ] Add text similarity for documents
- [ ] Create duplicate resolution UI suggestions
- [ ] Implement "keep original, delete copies" logic

### 2.5 Basic File Manager UI
- [ ] Create `FileManagerView.xaml` main view
- [ ] Create `FileListViewModel.cs` for file grid
- [ ] Implement folder navigation
- [ ] Add breadcrumb navigation control
- [ ] Add drive selector dropdown
- [ ] Implement file selection (single, multi)
- [ ] Add context menu (copy, cut, paste, delete)
- [ ] Add double-click to open
- [ ] Add keyboard navigation (arrows, enter, backspace)
- [ ] Show file icons based on type
- [ ] Add column sorting (name, size, date, type)

---

## Phase 3: Enhanced Features

### 3.1 Batch Rename Engine
- [ ] Create `BatchRenameService.cs` interface
- [ ] Implement find/replace rule
- [ ] Implement add prefix/suffix rule
- [ ] Implement counter/numbering rule
- [ ] Implement case change rule (lower, upper, title, sentence)
- [ ] Implement extension change rule
- [ ] Implement date insertion rule (from file date)
- [ ] Implement regex replace rule
- [ ] Implement metadata extraction (EXIF for images)
- [ ] Implement ID3 tag extraction (for audio)
- [ ] Create rule stacking (apply multiple rules in order)
- [ ] Create live preview (show before/after)
- [ ] Detect naming conflicts (duplicates)
- [ ] Implement undo for batch operations
- [ ] Create preset save/load functionality
- [ ] Add CLI support for batch rename

### 3.2 Watch Folder Automation
- [ ] Create `FolderWatcher` service using FileSystemWatcher
- [ ] Implement rule matching on new files
- [ ] Support multiple watch folders
- [ ] Add move/copy/rename actions
- [ ] Add file type filtering
- [ ] Add size threshold filtering
- [ ] Implement logging of actions
- [ ] Add scheduled vs real-time modes
- [ ] Create watch folder configuration UI
- [ ] Persist watch configurations

### 3.3 Context Menu Integration
- [ ] Create Windows shell extension project
- [ ] Register "Analyze with Winhance" menu item
- [ ] Register "Batch Rename with Winhance" menu item
- [ ] Register "Organize with Winhance" menu item
- [ ] Handle file/folder path passing
- [ ] Create installer/uninstaller for shell extension
- [ ] Add to Settings for enable/disable

### 3.4 Report Export
- [ ] Add CSV export for space analysis
- [ ] Add JSON export for space analysis
- [ ] Add HTML report generation with charts
- [ ] Add Excel export (using EPPlus or similar)
- [ ] Include summary statistics in reports
- [ ] Add charts/visualizations in HTML report
- [ ] Add command-line --output flag

---

## Phase 4: Epic Features

### 4.1 TreeMap Visualization
- [ ] Research WPF TreeMap controls (LiveCharts2, Syncfusion, custom)
- [ ] Create `TreeMapControl.xaml` UserControl
- [ ] Implement nested rectangle layout algorithm
- [ ] Add color coding by file type
- [ ] Add color coding by age (last accessed)
- [ ] Implement zoom in/out
- [ ] Implement click to drill down
- [ ] Add tooltip with file details
- [ ] Add right-click context menu
- [ ] Optimize rendering for large datasets

### 4.2 Natural Language Search
- [ ] Integrate sentence-transformers for embeddings
- [ ] Create embedding index using Qdrant/ChromaDB
- [ ] Parse natural language queries
- [ ] Extract intent (file type, date, location hints)
- [ ] Convert to search filters
- [ ] Implement hybrid search (keyword + semantic)
- [ ] Add "did you mean?" suggestions
- [ ] Cache common queries

### 4.3 Visual File Timeline
- [ ] Create timeline visualization control
- [ ] Group files by creation/modification date
- [ ] Show daily/weekly/monthly views
- [ ] Allow zoom in/out on time range
- [ ] Filter by file type
- [ ] Click to show files from that time
- [ ] Export timeline as image

### 4.4 Cloud Storage Analyzer
- [ ] Detect OneDrive sync folder
- [ ] Detect Dropbox folder
- [ ] Detect Google Drive folder
- [ ] Calculate local vs cloud-only files
- [ ] Find duplicates between cloud and local
- [ ] Show files available offline vs online-only
- [ ] Suggest files to move to cloud-only

### 4.5 Everything Search Integration
- [ ] Detect if Everything is installed
- [ ] Use Everything SDK/IPC if available
- [ ] Fall back to native search if not
- [ ] Show "powered by Everything" indicator
- [ ] Hybrid: use Everything for initial results, enhance with our metadata

---

## Phase 5: Polish & Integration

### 5.1 Performance Optimization
- [ ] Profile MFT reader performance
- [ ] Profile search latency
- [ ] Profile UI responsiveness
- [ ] Optimize memory usage for large indexes
- [ ] Add index compression
- [ ] Implement lazy loading for file lists
- [ ] Add virtualization for large lists

### 5.2 Error Handling
- [ ] Add comprehensive error messages
- [ ] Create error recovery procedures
- [ ] Add retry logic for transient failures
- [ ] Log errors to file
- [ ] Show user-friendly error dialogs
- [ ] Add "Report Bug" feature

### 5.3 Testing
- [ ] Create Rust unit tests for MFT parser
- [ ] Create Rust unit tests for search engine
- [ ] Create Python unit tests for space analyzer
- [ ] Create Python unit tests for transaction manager
- [ ] Create C# unit tests for ViewModels
- [ ] Create integration tests for MCP tools
- [ ] Create end-to-end tests

### 5.4 Documentation
- [ ] Update FEATURES.md with actual implemented features
- [ ] Create user guide with screenshots
- [ ] Create API documentation
- [ ] Create troubleshooting guide
- [ ] Record demo video
- [ ] Update README with accurate status

---

## 25 Epic Features Checklist

### Storage Intelligence (8 features)
- [ ] 1. Real-Time Storage Monitor (taskbar tray)
- [ ] 2. Predictive Storage Alerts (ML-based)
- [ ] 3. Cloud Storage Sync Analyzer
- [ ] 4. Steam/Epic Games Optimizer
- [ ] 5. Docker/WSL Image Pruner
- [ ] 6. Virtual Machine Storage Optimizer
- [ ] 7. Browser Profile Analyzer
- [ ] 8. Package Manager Cache Unifier

### Search & Discovery (7 features)
- [ ] 9. Natural Language Search
- [ ] 10. Visual File Timeline
- [ ] 11. Content-Aware Duplicate Finder
- [ ] 12. Orphan File Detector
- [ ] 13. Project Dependency Mapper
- [ ] 14. Registry-to-File Linker
- [ ] 15. Shortcut Validator

### Organization & Automation (6 features)
- [ ] 16. Smart Download Sorter
- [ ] 17. Screenshot Auto-Organizer (OCR)
- [ ] 18. Email Attachment Manager
- [ ] 19. Version Control Detector
- [ ] 20. Temporary Project Archiver
- [ ] 21. Media Library Consolidator

### Performance & Integration (6 features)
- [ ] 22. Everything Search Integration
- [ ] 23. Windows Search Index Enhancer
- [ ] 24. File Access Frequency Tracker
- [ ] 25. Multi-Machine Sync View

---

## Code Quality Checklist

### Rust (`nexus_core`)
- [ ] All public functions documented
- [ ] Error handling with `Result<T, NexusError>`
- [ ] No `unwrap()` in production code
- [ ] Proper logging with tracing
- [ ] Cargo clippy passes
- [ ] Cargo fmt applied

### Python (`nexus_ai`, `nexus_cli`, `nexus_mcp`)
- [ ] Type hints on all functions
- [ ] Docstrings on public functions
- [ ] Black formatting applied
- [ ] Ruff linting passes
- [ ] MyPy type checking passes
- [ ] No bare `except:` clauses

### C# (`Winhance.Core`, `Winhance.Infrastructure`, `Winhance.WPF`)
- [ ] Interfaces for all services
- [ ] OperationResult<T> pattern used
- [ ] Async methods use `Async` suffix
- [ ] Proper disposal with `using`
- [ ] MVVM pattern followed
- [ ] No code-behind logic in views

---

## File Structure To Create

```
src/
├── Winhance.Core/Features/
│   └── Storage/
│       ├── Interfaces/
│       │   ├── IStorageService.cs         [ ]
│       │   ├── IMftService.cs              [ ]
│       │   ├── ISearchService.cs           [ ]
│       │   ├── IBatchRenameService.cs      [ ]
│       │   └── IOrganizationService.cs     [ ]
│       ├── Models/
│       │   ├── DriveInfo.cs                [ ]
│       │   ├── FileEntry.cs                [ ]
│       │   ├── SearchResult.cs             [ ]
│       │   ├── SpaceRecoveryItem.cs        [ ]
│       │   └── RenameRule.cs               [ ]
│       └── Events/
│           ├── FileScanProgressEvent.cs    [ ]
│           └── SpaceAnalysisCompleteEvent.cs [ ]
│
├── Winhance.Infrastructure/Features/
│   └── Storage/
│       ├── Services/
│       │   ├── StorageService.cs           [ ]
│       │   ├── MftService.cs               [ ]
│       │   ├── SearchService.cs            [ ]
│       │   └── BatchRenameService.cs       [ ]
│       └── Native/
│           └── NexusNativeInterop.cs       [ ]
│
├── Winhance.WPF/Features/
│   └── Storage/
│       ├── ViewModels/
│       │   ├── StorageIntelligenceViewModel.cs  [ ]
│       │   ├── FileManagerViewModel.cs          [ ]
│       │   ├── BatchRenameViewModel.cs          [ ]
│       │   └── SmartOrganizerViewModel.cs       [ ]
│       ├── Views/
│       │   ├── StorageIntelligenceView.xaml     [ ]
│       │   ├── FileManagerView.xaml             [ ]
│       │   ├── BatchRenameView.xaml             [ ]
│       │   └── SmartOrganizerView.xaml          [ ]
│       └── Controls/
│           ├── TreeMapControl.xaml              [ ]
│           ├── DualPaneControl.xaml             [ ]
│           └── BreadcrumbNavigation.xaml        [ ]
│
└── nexus_core/src/
    ├── indexer/
    │   ├── mft_reader.rs       [~] needs implementation
    │   ├── usn_journal.rs      [~] needs implementation
    │   ├── metadata_extractor.rs [~] needs implementation
    │   └── content_hasher.rs   [~] needs implementation
    └── ffi/
        └── mod.rs              [ ] UniFFI exports
```

---

## Audit-Identified Gaps Summary

### Backend Completion Status

| Component                  | Current State | Priority |
| -------------------------- | ------------- | -------- |
| Rust Backend (nexus_core)  | 80% skeleton  | DEFERRED |
| Python AI Layer (nexus_ai) | 70% skeleton  | DEFERRED |
| C# Test Coverage           | 0%            | HIGH     |
| Security Fixes             | 18 issues     | CRITICAL |
| Error Handling             | 50+ issues    | CRITICAL |

### Files Requiring Immediate Attention

| File                            | Issues                        | Severity |
| ------------------------------- | ----------------------------- | -------- |
| OutputParser.cs                 | 7 bare catches                | CRITICAL |
| WindowsRegistryService.cs       | 12 silent failures + security | CRITICAL |
| WimUtilService.cs               | 6 bare catches + injection    | CRITICAL |
| MainViewModel.cs                | Event leaks, no IDisposable   | HIGH     |
| SoftwareAppsViewModel.cs        | Multiple issues               | HIGH     |
| TooltipRefreshEventHandler.cs   | async void                    | CRITICAL |
| BaseSettingsFeatureViewModel.cs | async void                    | CRITICAL |

### Related Documentation

- **[FAGAN_AUDIT_REPORT.md](FAGAN_AUDIT_REPORT.md)** - Complete audit findings
- **[REMEDIATION_ACTION_PLAN.md](REMEDIATION_ACTION_PLAN.md)** - Step-by-step fixes for AI IDE
- **[SECURITY_AUDIT.md](SECURITY_AUDIT.md)** - Security-specific findings
- **[CODE_QUALITY_STANDARDS.md](CODE_QUALITY_STANDARDS.md)** - Coding standards to follow

---

*Check items as you complete them. Update this document regularly.*
*Last Updated: January 22, 2026*
*Audit Integration: January 22, 2026*
