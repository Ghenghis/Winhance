"""
NexusFS Tools - Utility tools for file management.

Epic Features:
- SpaceAnalyzer: Disk space analysis with GPU acceleration
- ModelRelocator: AI model file management
- FileClassifier: Windows system file detection & safety
- RealtimeOrganizer: Agent-based file organization
- SmartFileManager: Touch/mouse seamless interface
"""

from nexus_ai.tools.space_analyzer import SpaceAnalyzer
from nexus_ai.tools.model_relocator import ModelRelocator

# Epic feature imports with fallback
try:
    from nexus_ai.tools.file_classifier import (
        FileClassifier,
        FileClassification,
        SafetyLevel,
        FileOrigin,
        WindowsSystemDetector,
        InstalledAppDetector,
        UserFileDetector,
        get_classifier,
        classify_file,
        is_safe_to_move,
        is_safe_to_delete,
    )
    _HAS_FILE_CLASSIFIER = True
except ImportError:
    _HAS_FILE_CLASSIFIER = False

try:
    from nexus_ai.tools.realtime_organizer import (
        RealtimeOrganizer,
        OrganizationRule,
        OrganizationDecision,
        OrganizationAction,
        ConfirmationLevel,
        UserPreference,
        get_organizer,
        start_organizing,
        stop_organizing,
    )
    _HAS_REALTIME_ORGANIZER = True
except ImportError:
    _HAS_REALTIME_ORGANIZER = False

try:
    from nexus_ai.tools.smart_filemanager import (
        SmartFileManager,
        GestureManager,
        QuickActionWheel,
        DropZoneManager,
        GestureType,
        ActionType,
        ViewMode,
        FileCard,
        QuickAction,
        DropZone,
        BreadcrumbItem,
        UndoAction,
        get_smart_manager,
    )
    _HAS_SMART_FILEMANAGER = True
except ImportError:
    _HAS_SMART_FILEMANAGER = False

__all__ = [
    # Core tools
    "SpaceAnalyzer",
    "ModelRelocator",
    # File classifier
    "FileClassifier",
    "FileClassification",
    "SafetyLevel",
    "FileOrigin",
    "WindowsSystemDetector",
    "InstalledAppDetector",
    "UserFileDetector",
    "get_classifier",
    "classify_file",
    "is_safe_to_move",
    "is_safe_to_delete",
    # Realtime organizer
    "RealtimeOrganizer",
    "OrganizationRule",
    "OrganizationDecision",
    "OrganizationAction",
    "ConfirmationLevel",
    "UserPreference",
    "get_organizer",
    "start_organizing",
    "stop_organizing",
    # Smart file manager
    "SmartFileManager",
    "GestureManager",
    "QuickActionWheel",
    "DropZoneManager",
    "GestureType",
    "ActionType",
    "ViewMode",
    "FileCard",
    "QuickAction",
    "DropZone",
    "BreadcrumbItem",
    "UndoAction",
    "get_smart_manager",
]
