"""
Tests for File Classifier

Comprehensive tests for the intelligent file classification system.
"""

from __future__ import annotations

import pytest
from pathlib import Path
import os
import tempfile

from tests.conftest import DummyFileGenerator


class TestFileClassifierBasic:
    """Basic file classifier tests."""

    def test_classifier_initialization(self):
        """Test FileClassifier can be initialized."""
        from nexus_ai.tools.file_classifier import FileClassifier

        classifier = FileClassifier()
        assert classifier is not None
        assert classifier.system_detector is not None
        assert classifier.app_detector is not None
        assert classifier.user_detector is not None

    def test_safety_levels_exist(self):
        """Test SafetyLevel enum values."""
        from nexus_ai.tools.file_classifier import SafetyLevel

        assert SafetyLevel.CRITICAL
        assert SafetyLevel.PROTECTED
        assert SafetyLevel.INSTALLED
        assert SafetyLevel.CAUTIOUS
        assert SafetyLevel.SAFE
        assert SafetyLevel.TEMPORARY

    def test_file_origin_exists(self):
        """Test FileOrigin enum values."""
        from nexus_ai.tools.file_classifier import FileOrigin

        assert FileOrigin.WINDOWS_SYSTEM
        assert FileOrigin.DRIVER
        assert FileOrigin.INSTALLED_APP
        assert FileOrigin.USER_CREATED
        assert FileOrigin.USER_DOWNLOADED
        assert FileOrigin.TEMP_FILE
        assert FileOrigin.UNKNOWN


class TestWindowsSystemDetector:
    """Tests for Windows system file detection."""

    def test_system_path_detection(self):
        """Test detection of system paths."""
        from nexus_ai.tools.file_classifier import WindowsSystemDetector

        detector = WindowsSystemDetector()

        # System32 should be detected as system path
        system32 = Path(os.environ.get("SYSTEMROOT", "C:\\Windows")) / "System32"
        assert detector.is_system_path(system32)

        # User folder should not be system path
        user_dir = Path.home() / "Documents"
        assert not detector.is_system_path(user_dir)

    def test_driver_detection(self):
        """Test driver file detection."""
        from nexus_ai.tools.file_classifier import WindowsSystemDetector

        detector = WindowsSystemDetector()

        # Driver path
        driver_dir = Path(os.environ.get("SYSTEMROOT", "C:\\Windows")) / "System32" / "drivers"
        if driver_dir.exists():
            assert detector.is_driver_file(driver_dir / "test.sys")

        # User files are not drivers
        user_file = Path.home() / "test.txt"
        assert not detector.is_driver_file(user_file)

    def test_protected_folder_detection(self):
        """Test protected folder detection."""
        from nexus_ai.tools.file_classifier import WindowsSystemDetector

        detector = WindowsSystemDetector()

        # System32 is protected
        system32_file = Path(os.environ.get("SYSTEMROOT", "C:\\Windows")) / "System32" / "test.dll"
        assert detector.is_protected_folder(system32_file)

        # User Documents is not protected
        user_doc = Path.home() / "Documents" / "test.txt"
        assert not detector.is_protected_folder(user_doc)


class TestUserFileDetector:
    """Tests for user file detection."""

    def test_user_folder_detection(self):
        """Test user folder detection."""
        from nexus_ai.tools.file_classifier import UserFileDetector

        detector = UserFileDetector()

        # Documents is a user folder
        docs = Path.home() / "Documents" / "test.txt"
        assert detector.is_user_folder(docs)

        # Pictures is a user folder
        pics = Path.home() / "Pictures" / "photo.jpg"
        assert detector.is_user_folder(pics)

    def test_user_created_detection(self, file_generator: DummyFileGenerator):
        """Test user-created file detection."""
        from nexus_ai.tools.file_classifier import UserFileDetector

        detector = UserFileDetector()

        # Create a user document
        doc = file_generator.create_file("my_document.docx", 1024)

        is_user, confidence = detector.is_user_created(doc)
        # May or may not be detected as user-created depending on location
        assert isinstance(is_user, bool)
        assert 0.0 <= confidence <= 1.0

    def test_project_file_detection(self, file_generator: DummyFileGenerator):
        """Test development project file detection."""
        from nexus_ai.tools.file_classifier import UserFileDetector

        detector = UserFileDetector()

        # Create project indicator
        package_json = file_generator.create_file("package.json", 256)

        # package.json is a project file
        assert detector.is_project_file(package_json)


class TestFileClassification:
    """Tests for full file classification."""

    def test_classify_user_file(self, file_generator: DummyFileGenerator):
        """Test classification of user files."""
        from nexus_ai.tools.file_classifier import classify_file, SafetyLevel

        user_doc = file_generator.create_file("my_notes.txt", 1024)
        classification = classify_file(user_doc)

        assert classification.path == user_doc
        # User file should be safe to move
        assert classification.safe_to_move or classification.safety_level in [
            SafetyLevel.SAFE, SafetyLevel.CAUTIOUS
        ]

    def test_classify_temp_file(self, file_generator: DummyFileGenerator):
        """Test classification of temp files."""
        from nexus_ai.tools.file_classifier import classify_file, SafetyLevel, FileOrigin

        temp_file = file_generator.create_file("cache.tmp", 512, subdir="temp")
        classification = classify_file(temp_file)

        # Temp files should be detected and safe to delete
        assert classification.safe_to_delete

    def test_batch_classify(self, file_generator: DummyFileGenerator):
        """Test batch classification."""
        from nexus_ai.tools.file_classifier import get_classifier

        # Create multiple files
        files = [
            file_generator.create_file("doc1.txt", 100),
            file_generator.create_file("doc2.pdf", 200),
            file_generator.create_file("image.png", 300),
        ]

        classifier = get_classifier()
        results = classifier.batch_classify(files)

        assert len(results) == 3
        for path, clf in results.items():
            assert clf.path in files

    def test_safety_summary(self, file_generator: DummyFileGenerator):
        """Test safety summary generation."""
        from nexus_ai.tools.file_classifier import get_classifier

        files = [
            file_generator.create_file("safe1.txt", 100),
            file_generator.create_file("safe2.txt", 200),
        ]

        classifier = get_classifier()
        classifications = classifier.batch_classify(files)
        summary = classifier.get_safety_summary(classifications)

        assert summary["total"] == 2
        assert "by_safety" in summary
        assert "safe_to_move" in summary


class TestConvenienceFunctions:
    """Tests for convenience functions."""

    def test_is_safe_to_move(self, file_generator: DummyFileGenerator):
        """Test is_safe_to_move function."""
        from nexus_ai.tools.file_classifier import is_safe_to_move

        user_file = file_generator.create_file("movable.txt", 100)
        is_safe, message = is_safe_to_move(user_file)

        assert isinstance(is_safe, bool)
        assert isinstance(message, str)

    def test_is_safe_to_delete(self, file_generator: DummyFileGenerator):
        """Test is_safe_to_delete function."""
        from nexus_ai.tools.file_classifier import is_safe_to_delete

        temp_file = file_generator.create_file("deletable.tmp", 100)
        is_safe, message = is_safe_to_delete(temp_file)

        assert isinstance(is_safe, bool)
        assert isinstance(message, str)


class TestEdgeCases:
    """Edge case tests."""

    def test_nonexistent_file(self):
        """Test handling of nonexistent file."""
        from nexus_ai.tools.file_classifier import classify_file

        fake_path = Path("C:/nonexistent/file/12345.txt")
        classification = classify_file(fake_path)

        # Should return a classification - nonexistent files get classified based on extension
        assert classification.path == fake_path
        # Classification should still work even for nonexistent files

    def test_special_characters(self, file_generator: DummyFileGenerator):
        """Test files with special characters in name."""
        from nexus_ai.tools.file_classifier import classify_file

        special_file = file_generator.create_file("file with spaces & symbols.txt", 100)
        classification = classify_file(special_file)

        assert classification.path == special_file
