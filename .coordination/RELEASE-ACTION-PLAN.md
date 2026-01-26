# RELEASE ACTION PLAN - Complete Winhance-FS for Release

**Created:** 2026-01-26
**Objective:** Complete all tasks for both Claude (Backend) and Windsurf (GUI/CLI) releases
**Status:** Backend ✅ Complete, In Progress - Fixing final issues

---

## CURRENT STATUS

### Claude (Backend) - 99% COMPLETE
- ✅ All 28 Interfaces created and verified
- ✅ All 28 Services implemented 
- ✅ All services registered in DI
- ✅ Backend compiles successfully
- ⚠️ Need to verify all services resolve from DI
- ⚠️ Need to update documentation

### Windsurf (GUI/CLI) - 0% COMPLETE
- ❌ No ViewModels created
- ❌ No XAML Views created
- ❌ No CLI commands implemented
- ❌ No controls or converters

---

## PHASE 1: FINALIZE CLAUDE BACKEND (1-2 hours)

### 1.1 Verify DI Container Resolution
```bash
# Create test to verify all services resolve
dotnet test src/Winhance.Infrastructure.Tests/Integration/DITest.cs
```

### 1.2 Update Documentation
- Update CLAUDE-BACKEND-STATUS.md with "100% COMPLETE"
- Update to-windsurf.md with "Backend Ready for Integration"
- Create RELEASE-NOTES.md for backend

### 1.3 Create Backend Release Tag
```bash
git add -A
git commit -m "Claude Backend v0.1.0 - All 28 services complete"
git tag -a v0.1.0-backend -m "Backend release - All services implemented"
git push origin main
git push origin v0.1.0-backend
```

---

## PHASE 2: WINDSURF GUI IMPLEMENTATION (2-3 days)

### 2.1 Core Infrastructure (Day 1)

#### 2.1.1 Create Base Classes
- [ ] Create `BaseViewModel.cs` in `src/Winhance.WPF/Features/Common/`
- [ ] Create `BaseView.cs` for common view functionality
- [ ] Create `RelayCommand.cs` and `AsyncRelayCommand.cs`

#### 2.1.2 Create Converters
- [ ] `BoolToVisibilityConverter.cs`
- [ ] `FileSizeConverter.cs`
- [ ] `DateTimeConverter.cs`
- [ ] `PathToFileNameConverter.cs`
- [ ] `IsDirectoryToImageConverter.cs`

### 2.2 Essential Views (Day 1-2)

#### 2.2.1 Dual Pane Browser (Priority 1)
- [ ] `DualPaneBrowserView.xaml` - Main window layout
- [ ] `DualPaneBrowserViewModel.cs` - Core logic
- [ ] `FileListView.xaml` - File list control
- [ ] `FileListViewModel.cs` - File list logic

#### 2.2.2 Navigation Components
- [ ] `AddressBarView.xaml` - Breadcrumb navigation
- [ ] `AddressBarViewModel.cs`
- [ ] `TabContainerView.xaml` - Tab control
- [ ] `TabContainerViewModel.cs`

#### 2.2.3 Core Services Integration
```csharp
// Example DualPaneBrowserViewModel
public class DualPaneBrowserViewModel : BaseViewModel
{
    private readonly IFileManagerService _fileManager;
    private readonly ISelectionService _selection;
    private readonly IClipboardService _clipboard;
    private readonly ITabService _tabService;
    private readonly IAddressBarService _addressBar;
    
    // Inject all required services
    public DualPaneBrowserViewModel(
        IFileManagerService fileManager,
        ISelectionService selection,
        IClipboardService clipboard,
        ITabService tabService,
        IAddressBarService addressBar)
    {
        _fileManager = fileManager;
        _selection = selection;
        _clipboard = clipboard;
        _tabService = tabService;
        _addressBar = addressBar;
        
        // Initialize commands
        CopyCommand = new RelayCommand(() => _clipboard.Copy(_selection.SelectedItems));
        PasteCommand = new AsyncRelayCommand(async () => await _fileManager.PasteAsync(CurrentPath));
        // ... more commands
    }
}
```

### 2.3 Essential Features (Day 2-3)

#### 2.3.1 File Operations
- [ ] Context menu implementation
- [ ] Drag and drop support
- [ ] Keyboard shortcuts
- [ ] Progress dialogs for operations

#### 2.3.2 Preview System
- [ ] `PreviewPanelView.xaml` - Right panel preview
- [ ] `PreviewPanelViewModel.cs`
- [ ] Quick look dialog (spacebar)

#### 2.3.3 Search System
- [ ] `SearchDialogView.xaml`
- [ ] `SearchDialogViewModel.cs`
- [ ] Integration with ISearchService

---

## PHASE 3: WINDSURF CLI IMPLEMENTATION (1 day)

### 3.1 CLI Commands Structure
```
src/Winhance.CLI/
├── Commands/
│   ├── CopyCommand.cs
│   ├── MoveCommand.cs
│   ├── DeleteCommand.cs
│   ├── ListCommand.cs
│   ├── SearchCommand.cs
│   └── SyncCommand.cs
├── Services/
│   └── CliOutputService.cs
└── Program.cs
```

### 3.2 Essential Commands
- [ ] `winhance copy <source> <dest>` - Copy files/folders
- [ ] `winhance move <source> <dest>` - Move files/folders
- [ ] `winhance delete <path>` - Delete with backup
- [ ] `winhance list <path>` - List directory contents
- [ ] `winhance search <pattern> <path>` - Search files
- [ ] `winhance sync <source> <dest>` - Sync folders

---

## PHASE 4: TESTING & VALIDATION (1 day)

### 4.1 Unit Tests
- [ ] Test all ViewModels
- [ ] Test CLI commands
- [ ] Test service integration

### 4.2 Integration Tests
- [ ] Test DI resolution
- [ ] Test file operations
- [ ] Test UI interactions

### 4.3 Manual Testing
- [ ] Test all 436 features from WINDSURF-MASTER-TASKS.md
- [ ] Test error handling
- [ ] Test performance

---

## PHASE 5: RELEASE PREPARATION (4 hours)

### 5.1 Final Documentation
- [ ] Update README.md with installation instructions
- [ ] Create USER-GUIDE.md
- [ ] Update CHANGELOG.md
- [ ] Create RELEASE-NOTES.md

### 5.2 Build & Package
```bash
# Build WPF application
dotnet publish src/Winhance.WPF -c Release -r win-x64 --self-contained

# Build CLI
dotnet publish src/Winhance.CLI -c Release -r win-x64 --self-contained

# Create installer
# TODO: Add WiX or Inno Setup configuration
```

### 5.3 Git Tag & Release
```bash
git add -A
git commit -m "Complete Winhance-FS v1.0.0 - All 436 features implemented"
git tag -a v1.0.0 -m "Full release with GUI and CLI"
git push origin main
git push origin v1.0.0

# Create GitHub Release with artifacts
gh release create v1.0.0 --title "Winhance-FS v1.0.0" --notes "Complete file manager with 436 features"
```

---

## DETAILED TASK CHECKLIST

### Claude Tasks (Backend)
- [ ] Verify all 28 services resolve from DI
- [ ] Run integration tests
- [ ] Update CLAUDE-BACKEND-STATUS.md to 100%
- [ ] Commit and tag backend release v0.1.0-backend

### Windsurf Tasks (GUI)
- [ ] Create base infrastructure (BaseViewModel, commands, converters)
- [ ] Implement DualPaneBrowserView and ViewModel
- [ ] Implement FileListView and ViewModel
- [ ] Implement AddressBarView and ViewModel
- [ ] Implement TabContainerView and ViewModel
- [ ] Implement PreviewPanelView and ViewModel
- [ ] Implement SearchDialogView and ViewModel
- [ ] Wire up all backend services
- [ ] Implement context menus
- [ ] Add keyboard shortcuts
- [ ] Add drag and drop
- [ ] Test all 47 dual-pane browser features
- [ ] Test all 73 file operation features
- [ ] Test all 56 preview features

### Windsurf Tasks (CLI)
- [ ] Create CLI project structure
- [ ] Implement 6 essential commands
- [ ] Add help system
- [ ] Add progress reporting
- [ ] Test all commands

### Documentation Tasks
- [ ] Update README.md
- [ ] Create installation guide
- [ ] Create user guide
- [ ] Document all 436 features
- [ ] Create API documentation

---

## SUCCESS CRITERIA

### Backend (Claude)
1. ✅ All 28 services compile
2. ✅ All services registered in DI
3. [ ] All services resolve correctly
4. [ ] Zero compilation errors
5. [ ] All tests pass

### GUI (Windsurf)
1. [ ] Application launches without errors
2. [ ] All 436 features functional
3. [ ] Memory usage < 100MB idle
4. [ ] File operations work correctly
5. [ ] No UI freezes or hangs

### CLI (Windsurf)
1. [ ] All commands execute without errors
2. [ ] Help system functional
3. [ ] Progress reporting works
4. [ ] Error handling functional

### Release
1. [ ] Installer created
2. [ ] Documentation complete
3. [ ] Git tag created
4. [ ] GitHub release published
5. [ ] Version number updated

---

## NEXT IMMEDIATE ACTIONS

1. **RIGHT NOW**: Verify DI resolution for all services
2. **NEXT HOUR**: Update documentation and commit backend release
3. **TODAY**: Start GUI implementation with DualPaneBrowserView
4. **THIS WEEK**: Complete all essential GUI features
5. **WEEKEND**: Complete CLI implementation
6. **NEXT WEEK**: Testing, documentation, and release

---

## NOTES

- Backend is 99% complete - just need final verification
- GUI implementation is the main effort required
- Focus on essential features first, then advanced features
- Test each component as you implement it
- Keep commits small and focused
- Document progress daily in coordination files
