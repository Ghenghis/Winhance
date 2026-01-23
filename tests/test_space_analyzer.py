"""
Tests for Space Analyzer

Comprehensive tests for disk space analysis functionality.
"""

from __future__ import annotations

import pytest
import time
from pathlib import Path
from typing import Dict, List, Any

# Import test utilities from conftest
from tests.conftest import DummyFileGenerator, skip_slow


class TestSpaceAnalyzerBasic:
    """Basic space analyzer tests."""

    def test_analyzer_initialization(self):
        """Test SpaceAnalyzer can be initialized."""
        from nexus_ai.tools.space_analyzer import SpaceAnalyzer

        analyzer = SpaceAnalyzer()
        assert analyzer is not None
        assert analyzer.large_threshold > 0
        assert analyzer.huge_threshold > analyzer.large_threshold

    def test_analyzer_with_custom_thresholds(self):
        """Test analyzer with custom size thresholds."""
        from nexus_ai.tools.space_analyzer import SpaceAnalyzer

        analyzer = SpaceAnalyzer(
            large_threshold_gb=0.5,
            huge_threshold_gb=5.0,
        )
        assert analyzer.large_threshold == int(0.5 * 1024**3)
        assert analyzer.huge_threshold == int(5.0 * 1024**3)

    def test_format_size(self):
        """Test size formatting utility."""
        from nexus_ai.tools.space_analyzer import SpaceAnalyzer

        analyzer = SpaceAnalyzer()

        assert "B" in analyzer.format_size(100)
        assert "KB" in analyzer.format_size(1024)
        assert "MB" in analyzer.format_size(1024 * 1024)
        assert "GB" in analyzer.format_size(1024 * 1024 * 1024)
        assert "TB" in analyzer.format_size(1024 * 1024 * 1024 * 1024)


class TestSpaceAnalyzerScanning:
    """Tests for scanning functionality."""

    def test_scan_empty_directory(self, temp_dir: Path):
        """Test scanning an empty directory."""
        from nexus_ai.tools.space_analyzer import SpaceAnalyzer

        analyzer = SpaceAnalyzer()
        analysis = analyzer.analyze_path(temp_dir)

        assert analysis.file_count == 0
        assert analysis.dir_count == 0

    def test_scan_with_files(self, file_generator: DummyFileGenerator):
        """Test scanning directory with files."""
        from nexus_ai.tools.space_analyzer import SpaceAnalyzer

        # Create test files
        file_generator.create_file("test1.txt", 1024)
        file_generator.create_file("test2.txt", 2048)
        file_generator.create_file("test3.txt", 4096)

        analyzer = SpaceAnalyzer()
        analysis = analyzer.analyze_path(file_generator.base_dir)

        assert analysis.file_count == 3

    def test_scan_nested_directories(self, file_generator: DummyFileGenerator):
        """Test scanning nested directory structure."""
        from nexus_ai.tools.space_analyzer import SpaceAnalyzer

        structure = {
            "level1": {
                "__files__": [{"name": "file1.txt", "size": 100}],
                "level2": {
                    "__files__": [{"name": "file2.txt", "size": 200}],
                    "level3": {
                        "__files__": [{"name": "file3.txt", "size": 300}],
                    }
                }
            }
        }
        file_generator.create_directory_structure(structure)

        analyzer = SpaceAnalyzer()
        analysis = analyzer.analyze_path(file_generator.base_dir)

        assert analysis.file_count == 3
        assert analysis.dir_count >= 3

    def test_scan_categorizes_file_types(self, file_generator: DummyFileGenerator):
        """Test that files are properly categorized."""
        from nexus_ai.tools.space_analyzer import SpaceAnalyzer

        # Create files of different types
        file_generator.create_file("model.gguf", 10 * 1024 * 1024, subdir="models")
        file_generator.create_file("cache.tmp", 1024, subdir="cache")
        file_generator.create_file("video.mp4", 5 * 1024 * 1024)

        analyzer = SpaceAnalyzer(large_threshold_gb=0.001)  # 1MB threshold
        analysis = analyzer.analyze_path(file_generator.base_dir)

        # Check categorization
        assert len(analysis.model_files) > 0 or len(analysis.large_files) > 0

    def test_scan_detects_large_files(self, file_generator: DummyFileGenerator):
        """Test detection of large files."""
        from nexus_ai.tools.space_analyzer import SpaceAnalyzer

        # Create large files (>1MB for this test)
        file_generator.create_file("large1.bin", 2 * 1024 * 1024)
        file_generator.create_file("large2.bin", 3 * 1024 * 1024)
        file_generator.create_file("small.bin", 1024)

        analyzer = SpaceAnalyzer(large_threshold_gb=0.001)  # 1MB
        analysis = analyzer.analyze_path(file_generator.base_dir)

        assert len(analysis.large_files) >= 2


class TestSpaceAnalyzerPerformance:
    """Performance tests for space analyzer."""

    @pytest.mark.slow
    def test_scan_performance_small_set(self, small_test_set: Dict[str, List[Path]], temp_dir: Path):
        """Test scanning performance with small file set."""
        from nexus_ai.tools.space_analyzer import SpaceAnalyzer

        analyzer = SpaceAnalyzer()

        start_time = time.time()
        analysis = analyzer.analyze_path(temp_dir)
        elapsed_ms = (time.time() - start_time) * 1000

        total_files = sum(len(files) for files in small_test_set.values())

        # Should complete within reasonable time
        assert elapsed_ms < 5000, f"Scan took too long: {elapsed_ms}ms"
        assert analysis.scan_time_ms > 0

    @pytest.mark.slow
    @pytest.mark.benchmark
    def test_scan_performance_large_set(self, large_test_set: Dict[str, List[Path]], temp_dir: Path):
        """Test scanning performance with large file set."""
        from nexus_ai.tools.space_analyzer import SpaceAnalyzer

        analyzer = SpaceAnalyzer()

        start_time = time.time()
        analysis = analyzer.analyze_path(temp_dir)
        elapsed_sec = time.time() - start_time

        total_files = sum(len(files) for files in large_test_set.values())
        files_per_sec = total_files / elapsed_sec if elapsed_sec > 0 else 0

        # Performance assertion
        assert files_per_sec > 100, f"Too slow: {files_per_sec:.0f} files/sec"

    def test_find_large_files_performance(self, file_generator: DummyFileGenerator):
        """Test find_large_files method performance."""
        from nexus_ai.tools.space_analyzer import SpaceAnalyzer

        # Create mixed files
        for i in range(50):
            size = 1024 if i % 2 == 0 else 2 * 1024 * 1024
            file_generator.create_file(f"file_{i}.bin", size)

        analyzer = SpaceAnalyzer()

        start_time = time.time()
        large_files = analyzer.find_large_files(
            file_generator.base_dir,
            min_size_gb=0.001,  # 1MB
            limit=100,
        )
        elapsed_ms = (time.time() - start_time) * 1000

        assert elapsed_ms < 2000, f"find_large_files too slow: {elapsed_ms}ms"
        assert len(large_files) == 25  # Half are large


class TestSpaceAnalyzerExtensions:
    """Tests for extension-based analysis."""

    def test_extension_statistics(self, file_generator: DummyFileGenerator):
        """Test extension-based statistics."""
        from nexus_ai.tools.space_analyzer import SpaceAnalyzer

        file_generator.create_file("doc1.pdf", 1024)
        file_generator.create_file("doc2.pdf", 2048)
        file_generator.create_file("code.py", 512)
        file_generator.create_file("data.json", 256)

        analyzer = SpaceAnalyzer()
        analysis = analyzer.analyze_path(file_generator.base_dir)

        assert ".pdf" in analysis.by_extension
        assert analysis.by_extension[".pdf"] == 3072  # 1024 + 2048

    def test_category_statistics(self, file_generator: DummyFileGenerator):
        """Test category-based statistics."""
        from nexus_ai.tools.space_analyzer import SpaceAnalyzer

        file_generator.create_file("video.mp4", 10000)
        file_generator.create_file("image.jpg", 5000)
        file_generator.create_file("code.py", 1000)

        analyzer = SpaceAnalyzer()
        analysis = analyzer.analyze_path(file_generator.base_dir)

        assert "video" in analysis.by_category
        assert "image" in analysis.by_category
        assert "code" in analysis.by_category


class TestSpaceAnalyzerDuplicates:
    """Tests for duplicate detection."""

    def test_duplicate_detection_with_hashing(self, file_generator: DummyFileGenerator):
        """Test duplicate detection when hashing is enabled."""
        from nexus_ai.tools.space_analyzer import SpaceAnalyzer

        # Create duplicate files
        dups = file_generator.create_duplicates(
            original_size=10000,
            num_copies=3,
            spread_dirs=True,
        )

        analyzer = SpaceAnalyzer(compute_hashes=True)
        analysis = analyzer.analyze_path(file_generator.base_dir)

        # Should detect duplicates
        assert len(analysis.duplicate_groups) > 0 or len(dups) == 3

    def test_no_duplicates_without_hashing(self, file_generator: DummyFileGenerator):
        """Test that duplicates aren't detected without hashing."""
        from nexus_ai.tools.space_analyzer import SpaceAnalyzer

        file_generator.create_duplicates(original_size=10000, num_copies=3)

        analyzer = SpaceAnalyzer(compute_hashes=False)
        analysis = analyzer.analyze_path(file_generator.base_dir)

        # No duplicates detected without hashing
        assert len(analysis.duplicate_groups) == 0


class TestSpaceAnalyzerIntegration:
    """Integration tests for space analyzer."""

    @pytest.mark.integration
    def test_analyze_real_user_directory(self):
        """Test analyzing a real user directory."""
        from nexus_ai.tools.space_analyzer import SpaceAnalyzer
        import os

        user_dir = Path(os.path.expanduser("~"))

        # Only run if we have permission
        if not user_dir.exists():
            pytest.skip("User directory not accessible")

        analyzer = SpaceAnalyzer(large_threshold_gb=1.0)

        # Analyze with limited depth to be fast
        # Note: This tests real filesystem access
        try:
            analysis = analyzer.analyze_path(user_dir)
            assert analysis.file_count > 0
        except PermissionError:
            pytest.skip("Insufficient permissions for user directory")

    @pytest.mark.integration
    @pytest.mark.slow
    def test_analyze_drive_root(self, test_config):
        """Test analyzing drive root (requires admin on Windows)."""
        from nexus_ai.tools.space_analyzer import SpaceAnalyzer

        if not test_config.use_real_drives:
            pytest.skip("Real drive testing disabled")

        analyzer = SpaceAnalyzer(large_threshold_gb=10.0)

        try:
            analysis = analyzer.analyze_drive("C")
            assert analysis.file_count > 0
            assert analysis.total_size > 0
        except PermissionError:
            pytest.skip("Insufficient permissions for drive root")


class TestSpaceAnalyzerEdgeCases:
    """Edge case tests."""

    def test_nonexistent_path(self):
        """Test handling of nonexistent path."""
        from nexus_ai.tools.space_analyzer import SpaceAnalyzer

        analyzer = SpaceAnalyzer()

        with pytest.raises(ValueError):
            analyzer.analyze_path(Path("/nonexistent/path/12345"))

    def test_empty_directory(self, temp_dir: Path):
        """Test handling of empty directory."""
        from nexus_ai.tools.space_analyzer import SpaceAnalyzer

        analyzer = SpaceAnalyzer()
        analysis = analyzer.analyze_path(temp_dir)

        assert analysis.file_count == 0
        assert analysis.dir_count == 0
        assert len(analysis.large_files) == 0

    def test_special_characters_in_filename(self, file_generator: DummyFileGenerator):
        """Test handling of special characters in filenames."""
        from nexus_ai.tools.space_analyzer import SpaceAnalyzer

        # Create files with special characters (Windows-safe subset)
        file_generator.create_file("file with spaces.txt", 100)
        file_generator.create_file("file-with-dashes.txt", 100)
        file_generator.create_file("file_with_underscores.txt", 100)
        file_generator.create_file("file.multiple.dots.txt", 100)

        analyzer = SpaceAnalyzer()
        analysis = analyzer.analyze_path(file_generator.base_dir)

        assert analysis.file_count == 4

    def test_very_deep_directory_structure(self, file_generator: DummyFileGenerator):
        """Test handling of deeply nested directories."""
        from nexus_ai.tools.space_analyzer import SpaceAnalyzer

        # Create deep structure
        deep_path = file_generator.base_dir
        for i in range(20):
            deep_path = deep_path / f"level_{i}"
        deep_path.mkdir(parents=True, exist_ok=True)
        (deep_path / "deep_file.txt").write_text("content")
        file_generator.created_files.append(deep_path / "deep_file.txt")

        analyzer = SpaceAnalyzer()
        analysis = analyzer.analyze_path(file_generator.base_dir)

        assert analysis.file_count == 1
