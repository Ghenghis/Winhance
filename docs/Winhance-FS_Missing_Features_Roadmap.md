Winhance-FS Missing Features Roadmap

Source: Comparative analysis of Winhance-FS.txt vs FEATURES.md
Goal: Parity with Spacedrive, Double Commander, and FilterTreeView.

1. Advanced Navigation & Layout

Current Status: Dual-pane and Tabs are implemented. The following are missing:

[ ] Split Orientation Toggle: Ability to switch split-screen from Vertical (side-by-side) to Horizontal (top-bottom).

[ ] Workspace Snapshots: Capability to Save/Load named window layouts (e.g., "Debug Layout" with specific tabs open in specific paths).

[ ] Pane Sync Mode: A "Follow" toggle where navigating a folder in the left pane automatically navigates the right pane to the same path (critical for comparison workflows).

[ ] Locked Panes: Ability to "Lock" a tab or pane to a specific directory to prevent accidental navigation.

[ ] Independent Pane History: Separate Back/Forward history stacks for Left vs. Right panes (Explorer shares history, which is annoying).

2. Professional Content Viewers

Current Status: Basic text/image preview implemented. Missing "Pro" inspectors:

Text & Code

[ ] Syntax Highlighting: Colorized view for code files (.rs, .cs, .py, .js, .json, .xml).

[ ] Hex View: Binary inspection mode for executables or unknown file types.

[ ] Encoding Detection: Auto-detect and toggle between UTF-8, ASCII, UTF-16, and ANSI.

Images

[ ] EXIF & Metadata Inspector: View camera data (ISO, Shutter, GPS) for images.

[ ] Histogram: Visual color distribution graph.

[ ] Perceptual Hash View: Visualize the "fingerprint" of an image used for duplicate detection.

[ ] Side-by-Side Compare: Select two images and view them split-screen with synchronized zoom.

Media

[ ] Audio Waveform: Visual waveform generated for audio files.

[ ] Video Scrubbing: Thumbnail-based timeline scrubbing for video files without opening a player.

3. Advanced Search & Filtering

Current Status: Deep Scan and Regex are implemented. Missing UI-centric filtering:

[ ] Filterable TreeView: A folder tree that dynamically filters/hides nodes as you type (Key feature of FilterTreeView).

[ ] Multi-Column Filters: Ability to filter by Size AND Date simultaneously (e.g., "> 100MB" AND "Modified Today").

[ ] Saved Search Profiles: Save complex regex/filter combinations as named presets.

[ ] Search History: Dropdown of recently used search queries.

4. Context Menus & Automation

Current Status: Basic context menu implemented. Missing scripting/customization:

[ ] Scriptable Context Entries: Allow users to add menu items that execute custom Python, Rust, or PowerShell scripts on the selected files.

[ ] Internal Command Routing: Menu items that execute Winhance internal logic (e.g., "Copy Path as JSON", "Calculate SHA256") without invoking the slow Windows Shell.

[ ] Safety-Aware Filtering: Option to visually dim or hide destructive operations (Delete, Move) to prevent accidents.

[ ] Command Palette (Ctrl+Shift+P): Keyboard-driven menu to search for any action in the app (Spacedrive style).

5. Archive Management

Current Status: Archives detected by organizer. Missing "First-Class" browsing:

[ ] Transparent Browsing: Navigate into .zip, .7z, .rar, and .tar files as if they were regular folders.

[ ] Direct Manipulation: Copy/Paste files into and out of archives without a manual extraction wizard.

[ ] Archive Diff: Compare the contents of an archive against a folder without extracting.

6. Metadata & Virtual Organization

Current Status: Smart Organizer (Rule-based) is implemented. Missing Manual/Virtual organization:

[ ] Manual Tagging: User-defined color tags or text labels stored in a database (independent of NTFS attributes).

[ ] Virtual Folders: Folders defined by queries (e.g., "All High Priority PDFs") rather than physical location.

[ ] Collections: Manual grouping of files from different drives into a single "Collection" view.

[ ] File Notes: Ability to attach text notes to specific files (stored in Winhance database).

7. Storage Control & Links

Current Status: Model Relocation implemented. Missing generic tools:

[ ] Symlink Visualizer: Visual indicator in the file list showing where a symlink points (and highlighting broken links).

[ ] GUI Link Creator: Interface to create Hard Links, Junctions, and Symlinks easily.

[ ] Storage Tiering Wizard: Generic tool to move any folder from SSD to HDD while leaving a symlink (not just AI models).

8. File Operations (Power User)

Current Status: Transaction system implemented. Missing queue management:

[ ] Background Operation Queue: A manager for file operations allowing Pause, Resume, and Reordering of copy jobs.

[ ] Verify After Copy: Checkbox to automatically hash-verify files after copying to ensure data integrity.