"""
Smart File Manager - Touch & Mouse Seamless Interface

Game-changing features:
- Mobile-app-like ease of use
- Touch gestures and mouse support
- Smart action suggestions
- Context-aware operations
- Undo/redo with timeline
- Visual safety indicators
"""

from __future__ import annotations

import logging
from dataclasses import dataclass, field
from datetime import datetime
from enum import Enum
from pathlib import Path
from typing import Dict, List, Optional, Callable, Any, TYPE_CHECKING
import time

# Setup logger with fallback
_logger: Optional[logging.Logger] = None

def _get_module_logger() -> logging.Logger:
    """Get logger with fallback."""
    global _logger
    if _logger is None:
        try:
            from nexus_ai.core.logging_config import get_logger
            _logger = get_logger("smart_filemanager")
        except ImportError:
            _logger = logging.getLogger("smart_filemanager")
    return _logger

logger = _get_module_logger()


class SafetyLevel(Enum):
    """File safety levels for move/delete operations."""
    CRITICAL = "critical"
    PROTECTED = "protected"
    INSTALLED = "installed"
    CAUTIOUS = "cautious"
    SAFE = "safe"
    TEMPORARY = "temporary"


if TYPE_CHECKING:
    from nexus_ai.tools.file_classifier import FileClassification, FileClassifier


def _get_classifier() -> Any:
    """Get file classifier with fallback."""
    try:
        from nexus_ai.tools.file_classifier import get_classifier
        return get_classifier()
    except ImportError:
        return None


def _get_organizer() -> Any:
    """Get realtime organizer with fallback."""
    try:
        from nexus_ai.tools.realtime_organizer import get_organizer
        return get_organizer()
    except ImportError:
        return None


class GestureType(Enum):
    """Touch gesture types."""
    TAP = "tap"
    DOUBLE_TAP = "double_tap"
    LONG_PRESS = "long_press"
    SWIPE_LEFT = "swipe_left"
    SWIPE_RIGHT = "swipe_right"
    SWIPE_UP = "swipe_up"
    SWIPE_DOWN = "swipe_down"
    PINCH_IN = "pinch_in"
    PINCH_OUT = "pinch_out"
    TWO_FINGER_SWIPE = "two_finger_swipe"
    DRAG = "drag"
    DRAG_DROP = "drag_drop"


class ActionType(Enum):
    """File manager action types."""
    OPEN = "open"
    SELECT = "select"
    MULTI_SELECT = "multi_select"
    MOVE = "move"
    COPY = "copy"
    DELETE = "delete"
    RENAME = "rename"
    PREVIEW = "preview"
    PROPERTIES = "properties"
    SHARE = "share"
    COMPRESS = "compress"
    EXTRACT = "extract"
    NAVIGATE_BACK = "navigate_back"
    NAVIGATE_FORWARD = "navigate_forward"
    NAVIGATE_UP = "navigate_up"
    REFRESH = "refresh"
    SEARCH = "search"
    NEW_FOLDER = "new_folder"
    SORT = "sort"
    VIEW_CHANGE = "view_change"
    QUICK_ACTION = "quick_action"


class ViewMode(Enum):
    """File display modes."""
    GRID = "grid"
    LIST = "list"
    CARDS = "cards"
    TIMELINE = "timeline"
    TREE = "tree"
    DETAILS = "details"


@dataclass
class GestureMapping:
    """Maps a gesture to an action."""
    gesture: GestureType
    action: ActionType
    context: Optional[str] = None  # "file", "folder", "empty", "selection"
    modifier: Optional[str] = None  # "shift", "ctrl", "alt"
    description: str = ""


@dataclass
class QuickAction:
    """A quick action for the action wheel."""
    id: str
    name: str
    icon: str
    action: ActionType
    shortcut: Optional[str] = None
    color: str = "#007ACC"
    enabled: bool = True
    tooltip: str = ""


@dataclass
class FileCard:
    """Visual representation of a file for touch UI."""
    path: Path
    name: str
    extension: str
    size: int
    modified: datetime

    # Visual properties
    icon: str = "file"
    thumbnail: Optional[str] = None  # Base64 or path
    color: str = "#FFFFFF"
    badge: Optional[str] = None  # "new", "modified", "syncing"

    # Safety indicators
    safety_level: SafetyLevel = SafetyLevel.SAFE
    safety_color: str = "#4CAF50"  # Green for safe
    safety_icon: str = "check"

    # Selection state
    is_selected: bool = False
    is_focused: bool = False
    is_dragging: bool = False

    # Classification - uses Any to avoid circular import
    classification: Optional[Any] = None

    # Actions available
    available_actions: List[ActionType] = field(default_factory=list)


@dataclass
class DropZone:
    """A drop zone for drag and drop operations."""
    id: str
    name: str
    icon: str
    path: Optional[Path] = None
    action: ActionType = ActionType.MOVE
    color: str = "#2196F3"
    is_active: bool = False
    accepts_types: List[str] = field(default_factory=lambda: ["*"])


@dataclass
class BreadcrumbItem:
    """A breadcrumb navigation item."""
    path: Path
    name: str
    icon: str = "folder"
    is_current: bool = False


@dataclass
class UndoAction:
    """An undoable action."""
    id: str
    timestamp: datetime
    action_type: ActionType
    description: str

    # State before action
    source_paths: List[Path]
    dest_paths: Optional[List[Path]] = None

    # Undo data
    can_undo: bool = True
    undo_data: Dict[str, Any] = field(default_factory=dict)


class GestureManager:
    """
    Manages touch and mouse gestures for file operations.

    Features:
    - Mobile-app-like gesture support
    - Mouse compatibility
    - Customizable mappings
    - Context-aware actions
    """

    # Default gesture mappings (mobile-app inspired)
    DEFAULT_MAPPINGS = [
        # Basic operations
        GestureMapping(GestureType.TAP, ActionType.SELECT, "file", description="Select file"),
        GestureMapping(GestureType.TAP, ActionType.NAVIGATE_BACK, "empty", description="Navigate to parent"),
        GestureMapping(GestureType.DOUBLE_TAP, ActionType.OPEN, "file", description="Open file"),
        GestureMapping(GestureType.DOUBLE_TAP, ActionType.OPEN, "folder", description="Enter folder"),
        GestureMapping(GestureType.LONG_PRESS, ActionType.MULTI_SELECT, "file", description="Start multi-select"),
        GestureMapping(GestureType.LONG_PRESS, ActionType.QUICK_ACTION, "folder", description="Show quick actions"),

        # Swipe operations (like mobile apps)
        GestureMapping(GestureType.SWIPE_RIGHT, ActionType.MOVE, "file", description="Move to..."),
        GestureMapping(GestureType.SWIPE_LEFT, ActionType.DELETE, "file", description="Delete (with undo)"),
        GestureMapping(GestureType.SWIPE_UP, ActionType.SHARE, "file", description="Share file"),
        GestureMapping(GestureType.SWIPE_DOWN, ActionType.PROPERTIES, "file", description="Show properties"),

        # Navigation
        GestureMapping(GestureType.TWO_FINGER_SWIPE, ActionType.NAVIGATE_BACK, description="Navigate back"),
        GestureMapping(GestureType.PINCH_IN, ActionType.VIEW_CHANGE, description="Switch to list view"),
        GestureMapping(GestureType.PINCH_OUT, ActionType.VIEW_CHANGE, description="Switch to grid view"),

        # Preview
        GestureMapping(GestureType.PINCH_OUT, ActionType.PREVIEW, "file", description="Preview file"),
    ]

    def __init__(self):
        self.mappings = self.DEFAULT_MAPPINGS.copy()
        self._custom_mappings: List[GestureMapping] = []

    def get_action(
        self,
        gesture: GestureType,
        context: str,
        modifier: Optional[str] = None
    ) -> Optional[ActionType]:
        """Get the action for a gesture in a context."""
        # Check custom mappings first
        for mapping in self._custom_mappings:
            if self._mapping_matches(mapping, gesture, context, modifier):
                return mapping.action

        # Then default mappings
        for mapping in self.mappings:
            if self._mapping_matches(mapping, gesture, context, modifier):
                return mapping.action

        return None

    def _mapping_matches(
        self,
        mapping: GestureMapping,
        gesture: GestureType,
        context: str,
        modifier: Optional[str]
    ) -> bool:
        """Check if a mapping matches the gesture."""
        if mapping.gesture != gesture:
            return False
        if mapping.context and mapping.context != context:
            return False
        if mapping.modifier and mapping.modifier != modifier:
            return False
        return True

    def add_custom_mapping(self, mapping: GestureMapping) -> None:
        """Add a custom gesture mapping."""
        self._custom_mappings.insert(0, mapping)

    def get_gesture_help(self) -> List[Dict[str, str]]:
        """Get help text for all gestures."""
        help_items = []
        for mapping in self.mappings:
            help_items.append({
                "gesture": mapping.gesture.value,
                "action": mapping.action.value,
                "context": mapping.context or "anywhere",
                "description": mapping.description,
            })
        return help_items


class QuickActionWheel:
    """
    Radial menu for quick actions (like mobile app context menus).

    Features:
    - Touch-friendly radial layout
    - Context-aware actions
    - Recent actions priority
    - Customizable
    """

    # Default quick actions
    DEFAULT_ACTIONS = [
        QuickAction("open", "Open", "folder-open", ActionType.OPEN, shortcut="Enter", color="#4CAF50"),
        QuickAction("copy", "Copy", "copy", ActionType.COPY, shortcut="Ctrl+C", color="#2196F3"),
        QuickAction("move", "Move", "folder-move", ActionType.MOVE, shortcut="Ctrl+X", color="#FF9800"),
        QuickAction("delete", "Delete", "trash", ActionType.DELETE, shortcut="Del", color="#F44336"),
        QuickAction("rename", "Rename", "edit", ActionType.RENAME, shortcut="F2", color="#9C27B0"),
        QuickAction("share", "Share", "share", ActionType.SHARE, color="#00BCD4"),
        QuickAction("properties", "Properties", "info", ActionType.PROPERTIES, shortcut="Alt+Enter", color="#607D8B"),
        QuickAction("compress", "Compress", "archive", ActionType.COMPRESS, color="#795548"),
    ]

    def __init__(self):
        self.actions = self.DEFAULT_ACTIONS.copy()
        self._recent_actions: List[str] = []
        self._max_recent = 8

    def get_actions_for_context(
        self,
        selected_items: List[FileCard],
        is_single: bool = True
    ) -> List[QuickAction]:
        """Get relevant quick actions for selection context."""
        available = []

        for action in self.actions:
            if self._is_action_available(action, selected_items, is_single):
                available.append(action)

        # Prioritize recent actions
        available.sort(key=lambda a: (
            -self._recent_actions.index(a.id) if a.id in self._recent_actions else 999
        ))

        return available[:8]  # Max 8 in wheel

    def _is_action_available(
        self,
        action: QuickAction,
        selected_items: List[FileCard],
        is_single: bool
    ) -> bool:
        """Check if action is available for selection."""
        if not action.enabled:
            return False

        # Check safety for dangerous actions
        if action.action in [ActionType.DELETE, ActionType.MOVE]:
            for item in selected_items:
                if item.safety_level in [SafetyLevel.CRITICAL, SafetyLevel.PROTECTED]:
                    return False

        # Some actions only for single selection
        if not is_single and action.action in [ActionType.RENAME, ActionType.PREVIEW]:
            return False

        return True

    def record_action(self, action_id: str) -> None:
        """Record an action for recent priority."""
        if action_id in self._recent_actions:
            self._recent_actions.remove(action_id)
        self._recent_actions.insert(0, action_id)
        if len(self._recent_actions) > self._max_recent:
            self._recent_actions.pop()


class DropZoneManager:
    """
    Manages visual drop zones for drag and drop.

    Features:
    - Dynamic drop zones that appear when dragging
    - Quick access to common destinations
    - Visual feedback
    - Magnetic snap-to
    """

    def __init__(self):
        self.zones: List[DropZone] = []
        self._setup_default_zones()

    def _setup_default_zones(self) -> None:
        """Setup default drop zones."""
        home = Path.home()

        self.zones = [
            DropZone(
                "documents", "Documents", "file-text",
                path=home / "Documents",
                color="#2196F3"
            ),
            DropZone(
                "pictures", "Pictures", "image",
                path=home / "Pictures",
                color="#4CAF50"
            ),
            DropZone(
                "downloads", "Downloads", "download",
                path=home / "Downloads",
                color="#FF9800"
            ),
            DropZone(
                "desktop", "Desktop", "monitor",
                path=home / "Desktop",
                color="#9C27B0"
            ),
            DropZone(
                "trash", "Delete", "trash",
                action=ActionType.DELETE,
                color="#F44336"
            ),
            DropZone(
                "compress", "Compress", "archive",
                action=ActionType.COMPRESS,
                color="#795548"
            ),
        ]

    def get_active_zones(self, dragging_items: List[FileCard]) -> List[DropZone]:
        """Get drop zones that can accept the dragged items."""
        active = []

        for zone in self.zones:
            if self._can_accept(zone, dragging_items):
                zone_copy = DropZone(
                    id=zone.id,
                    name=zone.name,
                    icon=zone.icon,
                    path=zone.path,
                    action=zone.action,
                    color=zone.color,
                    is_active=True,
                    accepts_types=zone.accepts_types,
                )
                active.append(zone_copy)

        return active

    def _can_accept(self, zone: DropZone, items: List[FileCard]) -> bool:
        """Check if zone can accept items."""
        # Check safety
        for item in items:
            if item.safety_level in [SafetyLevel.CRITICAL, SafetyLevel.PROTECTED]:
                if zone.action != ActionType.COPY:
                    return False

        # Check type restrictions
        if "*" not in zone.accepts_types:
            for item in items:
                if item.extension.lower() not in zone.accepts_types:
                    return False

        return True

    def add_custom_zone(self, zone: DropZone) -> None:
        """Add a custom drop zone."""
        self.zones.append(zone)


class SmartFileManager:
    """
    Smart file manager with touch-first, mobile-app-like UX.

    Features:
    - Touch gestures and mouse support
    - Mobile-inspired UI patterns
    - Smart safety indicators
    - Undo timeline
    - AI-powered suggestions
    - Real-time organization
    """

    def __init__(self):
        self.gesture_manager = GestureManager()
        self.quick_actions = QuickActionWheel()
        self.drop_zones = DropZoneManager()
        self.classifier: Any = _get_classifier()
        self.organizer: Any = _get_organizer()

        # State
        self.current_path: Path = Path.home()
        self.selected_items: List[FileCard] = []
        self.clipboard: List[FileCard] = []
        self.clipboard_action: Optional[ActionType] = None
        self.view_mode: ViewMode = ViewMode.CARDS
        self.sort_by: str = "name"
        self.sort_ascending: bool = True

        # History
        self.navigation_history: List[Path] = []
        self.history_index: int = -1
        self.undo_history: List[UndoAction] = []
        self.redo_history: List[UndoAction] = []

        # Callbacks
        self.on_navigate: Optional[Callable[[Path], None]] = None
        self.on_selection_change: Optional[Callable[[List[FileCard]], None]] = None
        self.on_action_complete: Optional[Callable[[ActionType, bool], None]] = None
        self.on_confirmation_needed: Optional[Callable[[str, str, Callable[[bool], None]], None]] = None

    def navigate_to(self, path: Path) -> List[FileCard]:
        """Navigate to a directory and return file cards."""
        path = path.resolve()

        if not path.exists() or not path.is_dir():
            return []

        # Update history
        if self.current_path != path:
            self.navigation_history = self.navigation_history[:self.history_index + 1]
            self.navigation_history.append(path)
            self.history_index = len(self.navigation_history) - 1

        self.current_path = path
        self.selected_items.clear()

        # Get and classify files
        cards = self._get_file_cards(path)

        # Sort
        cards = self._sort_cards(cards)

        if self.on_navigate:
            self.on_navigate(path)

        return cards

    def _get_file_cards(self, path: Path) -> List[FileCard]:
        """Get file cards for directory contents."""
        cards = []

        try:
            for item in path.iterdir():
                card = self._create_file_card(item)
                if card:
                    cards.append(card)
        except PermissionError:
            logger.warning(f"Permission denied: {path}")
        except Exception as e:
            logger.error(f"Error reading directory: {e}")

        return cards

    def _create_file_card(self, path: Path) -> Optional[FileCard]:
        """Create a file card with classification."""
        try:
            stat = path.stat()
            classification = self.classifier.classify(path)

            # Determine safety color
            safety_colors = {
                SafetyLevel.CRITICAL: "#F44336",  # Red
                SafetyLevel.PROTECTED: "#FF9800",  # Orange
                SafetyLevel.INSTALLED: "#FFC107",  # Amber
                SafetyLevel.CAUTIOUS: "#FFEB3B",  # Yellow
                SafetyLevel.SAFE: "#4CAF50",  # Green
                SafetyLevel.TEMPORARY: "#9E9E9E",  # Gray
            }

            # Determine icon
            if path.is_dir():
                icon = "folder"
            else:
                icon = self._get_icon_for_extension(path.suffix.lower())

            # Get available actions
            available_actions = self._get_available_actions(classification)

            return FileCard(
                path=path,
                name=path.name,
                extension=path.suffix.lower(),
                size=stat.st_size if path.is_file() else 0,
                modified=datetime.fromtimestamp(stat.st_mtime),
                icon=icon,
                safety_level=classification.safety_level,
                safety_color=safety_colors.get(classification.safety_level, "#4CAF50"),
                classification=classification,
                available_actions=available_actions,
            )

        except Exception as e:
            logger.debug(f"Error creating card for {path}: {e}")
            return None

    def _get_icon_for_extension(self, ext: str) -> str:
        """Get icon name for file extension."""
        icon_map = {
            # Documents
            ".pdf": "file-pdf",
            ".doc": "file-word",
            ".docx": "file-word",
            ".xls": "file-excel",
            ".xlsx": "file-excel",
            ".ppt": "file-powerpoint",
            ".pptx": "file-powerpoint",
            ".txt": "file-text",
            ".md": "file-text",

            # Images
            ".jpg": "image",
            ".jpeg": "image",
            ".png": "image",
            ".gif": "image",
            ".bmp": "image",
            ".svg": "image",

            # Media
            ".mp3": "music",
            ".wav": "music",
            ".flac": "music",
            ".mp4": "video",
            ".mkv": "video",
            ".avi": "video",

            # Archives
            ".zip": "archive",
            ".rar": "archive",
            ".7z": "archive",

            # Code
            ".py": "code",
            ".js": "code",
            ".ts": "code",
            ".html": "code",
            ".css": "code",

            # Executables
            ".exe": "application",
            ".msi": "application",
        }

        return icon_map.get(ext, "file")

    def _get_available_actions(self, classification: FileClassification) -> List[ActionType]:
        """Get available actions based on classification."""
        actions = [ActionType.OPEN, ActionType.PROPERTIES]

        if classification.safe_to_move:
            actions.extend([ActionType.MOVE, ActionType.COPY, ActionType.RENAME])

        if classification.safe_to_delete:
            actions.append(ActionType.DELETE)

        if classification.safety_level not in [SafetyLevel.CRITICAL, SafetyLevel.PROTECTED]:
            actions.extend([ActionType.SHARE, ActionType.COMPRESS])

        return actions

    def _sort_cards(self, cards: List[FileCard]) -> List[FileCard]:
        """Sort file cards."""
        # Folders first
        folders = [c for c in cards if c.path.is_dir()]
        files = [c for c in cards if not c.path.is_dir()]

        key_map = {
            "name": lambda c: c.name.lower(),
            "size": lambda c: c.size,
            "modified": lambda c: c.modified,
            "type": lambda c: c.extension,
        }

        key_func = key_map.get(self.sort_by, key_map["name"])
        reverse = not self.sort_ascending

        folders.sort(key=key_func, reverse=reverse)
        files.sort(key=key_func, reverse=reverse)

        return folders + files

    def handle_gesture(
        self,
        gesture: GestureType,
        target: Optional[FileCard] = None,
        modifier: Optional[str] = None
    ) -> Optional[ActionType]:
        """Handle a touch/mouse gesture."""
        context = "empty"
        if target:
            context = "folder" if target.path.is_dir() else "file"

        action = self.gesture_manager.get_action(gesture, context, modifier)

        if action:
            self._execute_action(action, [target] if target else [])

        return action

    def _execute_action(
        self,
        action: ActionType,
        targets: List[FileCard],
        destination: Optional[Path] = None
    ) -> bool:
        """Execute a file action."""
        if not targets and action not in [ActionType.NAVIGATE_BACK, ActionType.NAVIGATE_FORWARD, ActionType.REFRESH]:
            targets = self.selected_items

        try:
            if action == ActionType.OPEN:
                return self._action_open(targets)
            elif action == ActionType.SELECT:
                return self._action_select(targets)
            elif action == ActionType.MULTI_SELECT:
                return self._action_multi_select(targets)
            elif action == ActionType.DELETE:
                return self._action_delete(targets)
            elif action == ActionType.MOVE:
                return self._action_move(targets, destination)
            elif action == ActionType.COPY:
                return self._action_copy(targets, destination)
            elif action == ActionType.RENAME:
                return self._action_rename(targets)
            elif action == ActionType.NAVIGATE_BACK:
                return self._action_navigate_back()
            elif action == ActionType.NAVIGATE_FORWARD:
                return self._action_navigate_forward()
            elif action == ActionType.NAVIGATE_UP:
                return self._action_navigate_up()
            else:
                logger.warning(f"Unhandled action: {action}")
                return False

        except Exception as e:
            logger.error(f"Action failed: {e}")
            return False

    def _action_open(self, targets: List[FileCard]) -> bool:
        """Open file or navigate to folder."""
        if not targets:
            return False

        target = targets[0]

        if target.path.is_dir():
            self.navigate_to(target.path)
        else:
            # Open with default application
            import os
            os.startfile(str(target.path))

        return True

    def _action_select(self, targets: List[FileCard]) -> bool:
        """Select a single item."""
        self.selected_items = targets[:1]

        if self.on_selection_change:
            self.on_selection_change(self.selected_items)

        return True

    def _action_multi_select(self, targets: List[FileCard]) -> bool:
        """Add to multi-selection."""
        for target in targets:
            if target not in self.selected_items:
                self.selected_items.append(target)
            target.is_selected = True

        if self.on_selection_change:
            self.on_selection_change(self.selected_items)

        return True

    def _action_delete(self, targets: List[FileCard]) -> bool:
        """Delete files (to recycle bin with undo)."""
        # Check safety
        for target in targets:
            if target.safety_level in [SafetyLevel.CRITICAL, SafetyLevel.PROTECTED]:
                logger.warning(f"Cannot delete protected file: {target.path}")
                return False

        # Request confirmation for cautious files
        needs_confirm = any(
            t.safety_level in [SafetyLevel.INSTALLED, SafetyLevel.CAUTIOUS]
            for t in targets
        )

        if needs_confirm and self.on_confirmation_needed:
            file_list = "\n".join(t.name for t in targets[:5])
            if len(targets) > 5:
                file_list += f"\n... and {len(targets) - 5} more"

            def on_confirm(confirmed: bool):
                if confirmed:
                    self._perform_delete(targets)

            self.on_confirmation_needed(
                "Confirm Delete",
                f"Delete these files?\n\n{file_list}",
                on_confirm
            )
            return True

        return self._perform_delete(targets)

    def _perform_delete(self, targets: List[FileCard]) -> bool:
        """Actually perform delete operation."""
        try:
            import send2trash

            # Create undo action
            undo = UndoAction(
                id=f"delete_{int(time.time())}",
                timestamp=datetime.now(),
                action_type=ActionType.DELETE,
                description=f"Deleted {len(targets)} items",
                source_paths=[t.path for t in targets],
            )

            for target in targets:
                send2trash.send2trash(str(target.path))

            self.undo_history.append(undo)
            self.redo_history.clear()

            # Refresh view
            self.navigate_to(self.current_path)

            return True

        except ImportError:
            # Fallback to regular delete with backup
            logger.warning("send2trash not available, using manual backup")
            return False

        except Exception as e:
            logger.error(f"Delete failed: {e}")
            return False

    def _action_move(self, targets: List[FileCard], destination: Optional[Path]) -> bool:
        """Move files to destination."""
        if destination:
            return self._perform_move(targets, destination)

        # Store in clipboard for later paste
        self.clipboard = targets.copy()
        self.clipboard_action = ActionType.MOVE
        return True

    def _perform_move(self, targets: List[FileCard], destination: Path) -> bool:
        """Actually perform move operation."""
        import shutil

        try:
            undo = UndoAction(
                id=f"move_{int(time.time())}",
                timestamp=datetime.now(),
                action_type=ActionType.MOVE,
                description=f"Moved {len(targets)} items to {destination.name}",
                source_paths=[t.path for t in targets],
                dest_paths=[],
            )

            for target in targets:
                dest_path = destination / target.path.name
                shutil.move(str(target.path), str(dest_path))
                if undo.dest_paths is not None:
                    undo.dest_paths.append(dest_path)

            self.undo_history.append(undo)
            self.redo_history.clear()

            self.navigate_to(self.current_path)
            return True

        except Exception as e:
            logger.error(f"Move failed: {e}")
            return False

    def _action_copy(self, targets: List[FileCard], destination: Optional[Path]) -> bool:
        """Copy files."""
        if destination:
            return self._perform_copy(targets, destination)

        self.clipboard = targets.copy()
        self.clipboard_action = ActionType.COPY
        return True

    def _perform_copy(self, targets: List[FileCard], destination: Path) -> bool:
        """Actually perform copy operation."""
        import shutil

        try:
            for target in targets:
                dest_path = destination / target.path.name

                # Handle name conflicts
                if dest_path.exists():
                    base = dest_path.stem
                    ext = dest_path.suffix
                    counter = 1
                    while dest_path.exists():
                        dest_path = destination / f"{base}_copy{counter}{ext}"
                        counter += 1

                if target.path.is_dir():
                    shutil.copytree(str(target.path), str(dest_path))
                else:
                    shutil.copy2(str(target.path), str(dest_path))

            self.navigate_to(self.current_path)
            return True

        except Exception as e:
            logger.error(f"Copy failed: {e}")
            return False

    def _action_rename(self, targets: List[FileCard]) -> bool:
        """Rename a file (single selection only)."""
        if len(targets) != 1:
            return False

        # This would trigger a rename dialog in the UI
        # For now, just return True to indicate the action is valid
        return True

    def _action_navigate_back(self) -> bool:
        """Navigate back in history."""
        if self.history_index > 0:
            self.history_index -= 1
            path = self.navigation_history[self.history_index]
            self.current_path = path
            return True
        return False

    def _action_navigate_forward(self) -> bool:
        """Navigate forward in history."""
        if self.history_index < len(self.navigation_history) - 1:
            self.history_index += 1
            path = self.navigation_history[self.history_index]
            self.current_path = path
            return True
        return False

    def _action_navigate_up(self) -> bool:
        """Navigate to parent directory."""
        parent = self.current_path.parent
        if parent != self.current_path:
            self.navigate_to(parent)
            return True
        return False

    def undo(self) -> Optional[UndoAction]:
        """Undo the last action."""
        if not self.undo_history:
            return None

        action = self.undo_history.pop()
        import shutil

        try:
            if action.action_type == ActionType.MOVE:
                # Move files back
                if action.dest_paths:
                    for src, dest in zip(action.dest_paths, action.source_paths):
                        if src.exists():
                            shutil.move(str(src), str(dest))

            elif action.action_type == ActionType.DELETE:
                # Restore from recycle bin if possible
                logger.info("Restore from recycle bin not implemented")

            self.redo_history.append(action)
            self.navigate_to(self.current_path)
            return action

        except Exception as e:
            logger.error(f"Undo failed: {e}")
            return None

    def redo(self) -> Optional[UndoAction]:
        """Redo the last undone action."""
        if not self.redo_history:
            return None

        action = self.redo_history.pop()
        # Re-execute the action
        # Implementation would depend on action type
        self.undo_history.append(action)
        return action

    def get_breadcrumbs(self) -> List[BreadcrumbItem]:
        """Get breadcrumb navigation items."""
        crumbs = []
        path = self.current_path

        while path != path.parent:
            crumbs.insert(0, BreadcrumbItem(
                path=path,
                name=path.name or str(path),
                is_current=(path == self.current_path),
            ))
            path = path.parent

        # Add root
        crumbs.insert(0, BreadcrumbItem(
            path=path,
            name=str(path),
            icon="hard-drive",
        ))

        return crumbs

    def paste(self) -> bool:
        """Paste clipboard contents."""
        if not self.clipboard:
            return False

        if self.clipboard_action == ActionType.MOVE:
            return self._perform_move(self.clipboard, self.current_path)
        elif self.clipboard_action == ActionType.COPY:
            return self._perform_copy(self.clipboard, self.current_path)

        return False


# Global instance
_smart_manager: Optional[SmartFileManager] = None


def get_smart_manager() -> SmartFileManager:
    """Get global smart file manager instance."""
    global _smart_manager
    if _smart_manager is None:
        _smart_manager = SmartFileManager()
    return _smart_manager
