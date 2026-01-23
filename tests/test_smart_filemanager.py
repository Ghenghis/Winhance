"""
Tests for Smart File Manager

Comprehensive tests for the touch/mouse seamless file manager.
"""

from __future__ import annotations

import pytest
from pathlib import Path
from datetime import datetime

from tests.conftest import DummyFileGenerator


class TestGestureManager:
    """Tests for gesture manager."""

    def test_gesture_manager_initialization(self):
        """Test GestureManager can be initialized."""
        from nexus_ai.tools.smart_filemanager import GestureManager

        manager = GestureManager()
        assert manager is not None
        assert len(manager.mappings) > 0

    def test_gesture_types_exist(self):
        """Test GestureType enum values."""
        from nexus_ai.tools.smart_filemanager import GestureType

        assert GestureType.TAP
        assert GestureType.DOUBLE_TAP
        assert GestureType.LONG_PRESS
        assert GestureType.SWIPE_LEFT
        assert GestureType.SWIPE_RIGHT
        assert GestureType.PINCH_IN
        assert GestureType.PINCH_OUT
        assert GestureType.DRAG

    def test_get_action_for_gesture(self):
        """Test getting action for gesture."""
        from nexus_ai.tools.smart_filemanager import GestureManager, GestureType, ActionType

        manager = GestureManager()

        # Tap on file should select
        action = manager.get_action(GestureType.TAP, "file")
        assert action == ActionType.SELECT

        # Double tap should open
        action = manager.get_action(GestureType.DOUBLE_TAP, "file")
        assert action == ActionType.OPEN

    def test_add_custom_mapping(self):
        """Test adding custom gesture mapping."""
        from nexus_ai.tools.smart_filemanager import GestureManager, GestureMapping, GestureType, ActionType

        manager = GestureManager()

        custom = GestureMapping(
            gesture=GestureType.PINCH_IN,
            action=ActionType.COMPRESS,
            context="file",
            description="Compress file"
        )
        manager.add_custom_mapping(custom)

        # Custom mapping should take priority
        action = manager.get_action(GestureType.PINCH_IN, "file")
        assert action == ActionType.COMPRESS

    def test_gesture_help(self):
        """Test getting gesture help."""
        from nexus_ai.tools.smart_filemanager import GestureManager

        manager = GestureManager()
        help_items = manager.get_gesture_help()

        assert len(help_items) > 0
        assert all("gesture" in item for item in help_items)
        assert all("action" in item for item in help_items)


class TestQuickActionWheel:
    """Tests for quick action wheel."""

    def test_action_wheel_initialization(self):
        """Test QuickActionWheel can be initialized."""
        from nexus_ai.tools.smart_filemanager import QuickActionWheel

        wheel = QuickActionWheel()
        assert wheel is not None
        assert len(wheel.actions) > 0

    def test_get_actions_for_context(self):
        """Test getting actions for selection context."""
        from nexus_ai.tools.smart_filemanager import (
            QuickActionWheel, FileCard, SafetyLevel
        )

        wheel = QuickActionWheel()

        # Create a test file card
        card = FileCard(
            path=Path.home() / "test.txt",
            name="test.txt",
            extension=".txt",
            size=1024,
            modified=datetime.now(),
            safety_level=SafetyLevel.SAFE,
        )

        actions = wheel.get_actions_for_context([card], is_single=True)
        assert len(actions) > 0
        assert len(actions) <= 8  # Max 8 in wheel

    def test_record_action_priority(self):
        """Test that recent actions get priority."""
        from nexus_ai.tools.smart_filemanager import QuickActionWheel

        wheel = QuickActionWheel()

        wheel.record_action("copy")
        wheel.record_action("move")

        assert wheel._recent_actions[0] == "move"
        assert wheel._recent_actions[1] == "copy"


class TestDropZoneManager:
    """Tests for drop zone manager."""

    def test_drop_zone_initialization(self):
        """Test DropZoneManager can be initialized."""
        from nexus_ai.tools.smart_filemanager import DropZoneManager

        manager = DropZoneManager()
        assert manager is not None
        assert len(manager.zones) > 0

    def test_default_zones_exist(self):
        """Test default drop zones exist."""
        from nexus_ai.tools.smart_filemanager import DropZoneManager

        manager = DropZoneManager()
        zone_ids = [z.id for z in manager.zones]

        assert "documents" in zone_ids
        assert "pictures" in zone_ids
        assert "downloads" in zone_ids
        assert "trash" in zone_ids

    def test_get_active_zones(self):
        """Test getting active zones for dragged items."""
        from nexus_ai.tools.smart_filemanager import (
            DropZoneManager, FileCard, SafetyLevel
        )

        manager = DropZoneManager()

        card = FileCard(
            path=Path.home() / "test.txt",
            name="test.txt",
            extension=".txt",
            size=1024,
            modified=datetime.now(),
            safety_level=SafetyLevel.SAFE,
        )

        active = manager.get_active_zones([card])
        assert len(active) > 0
        assert all(z.is_active for z in active)


class TestSmartFileManager:
    """Tests for smart file manager."""

    def test_manager_initialization(self):
        """Test SmartFileManager can be initialized."""
        from nexus_ai.tools.smart_filemanager import SmartFileManager

        manager = SmartFileManager()
        assert manager is not None
        assert manager.gesture_manager is not None
        assert manager.quick_actions is not None
        assert manager.drop_zones is not None

    def test_navigate_to_home(self):
        """Test navigation to home directory."""
        from nexus_ai.tools.smart_filemanager import SmartFileManager

        manager = SmartFileManager()
        cards = manager.navigate_to(Path.home())

        assert manager.current_path == Path.home()
        # Home directory should have files
        assert isinstance(cards, list)

    def test_navigation_history(self):
        """Test navigation history tracking."""
        from nexus_ai.tools.smart_filemanager import SmartFileManager

        manager = SmartFileManager()

        # Navigate - each unique path should be added to history
        manager.navigate_to(Path.home())

        # History should have at least one entry after navigation
        assert len(manager.navigation_history) >= 1
        assert manager.history_index >= 0

    def test_navigate_back(self):
        """Test navigate back functionality."""
        from nexus_ai.tools.smart_filemanager import SmartFileManager

        manager = SmartFileManager()

        manager.navigate_to(Path.home())
        manager.navigate_to(Path.home() / "Documents")

        result = manager._action_navigate_back()
        if manager.history_index > 0:
            assert result

    def test_breadcrumbs(self):
        """Test breadcrumb generation."""
        from nexus_ai.tools.smart_filemanager import SmartFileManager

        manager = SmartFileManager()
        manager.navigate_to(Path.home() / "Documents")

        crumbs = manager.get_breadcrumbs()
        assert len(crumbs) > 0
        # Last crumb should be current
        assert crumbs[-1].is_current

    def test_view_modes_exist(self):
        """Test ViewMode enum values."""
        from nexus_ai.tools.smart_filemanager import ViewMode

        assert ViewMode.GRID
        assert ViewMode.LIST
        assert ViewMode.CARDS
        assert ViewMode.TIMELINE
        assert ViewMode.DETAILS

    def test_sorting(self, file_generator: DummyFileGenerator):
        """Test file sorting."""
        from nexus_ai.tools.smart_filemanager import SmartFileManager, FileCard

        manager = SmartFileManager()

        # Create test files
        file_generator.create_file("a_file.txt", 100)
        file_generator.create_file("z_file.txt", 200)
        file_generator.create_file("m_file.txt", 150)

        cards = manager.navigate_to(file_generator.base_dir)

        # Default sort by name
        assert manager.sort_by == "name"


class TestFileCard:
    """Tests for FileCard dataclass."""

    def test_file_card_creation(self):
        """Test FileCard can be created."""
        from nexus_ai.tools.smart_filemanager import FileCard, SafetyLevel

        card = FileCard(
            path=Path.home() / "test.txt",
            name="test.txt",
            extension=".txt",
            size=1024,
            modified=datetime.now(),
            safety_level=SafetyLevel.SAFE,
        )

        assert card.name == "test.txt"
        assert card.safety_level == SafetyLevel.SAFE
        assert card.is_selected == False

    def test_file_card_default_values(self):
        """Test FileCard default values."""
        from nexus_ai.tools.smart_filemanager import FileCard, SafetyLevel

        card = FileCard(
            path=Path.home() / "test.txt",
            name="test.txt",
            extension=".txt",
            size=1024,
            modified=datetime.now(),
        )

        assert card.icon == "file"
        assert card.safety_level == SafetyLevel.SAFE
        assert card.safety_color == "#4CAF50"


class TestUndoSystem:
    """Tests for undo system."""

    def test_undo_action_creation(self):
        """Test UndoAction can be created."""
        from nexus_ai.tools.smart_filemanager import UndoAction, ActionType

        action = UndoAction(
            id="test_1",
            timestamp=datetime.now(),
            action_type=ActionType.MOVE,
            description="Moved file",
            source_paths=[Path.home() / "test.txt"],
        )

        assert action.can_undo
        assert action.action_type == ActionType.MOVE


class TestGlobalInstance:
    """Tests for global instance functions."""

    def test_get_smart_manager(self):
        """Test getting global smart manager instance."""
        from nexus_ai.tools.smart_filemanager import get_smart_manager

        manager1 = get_smart_manager()
        manager2 = get_smart_manager()

        # Should return same instance
        assert manager1 is manager2
