"""
PyTest Configuration and Fixtures

Provides:
- Test environment setup/teardown
- Dummy file generation for storage tests
- Mock AI providers
- Performance benchmarking utilities
"""

from __future__ import annotations

import os
import sys
import shutil
import tempfile
import random
import string
import hashlib
from pathlib import Path
from datetime import datetime, timedelta
from typing import Generator, Dict, Any, List, Optional
from dataclasses import dataclass
import asyncio

import pytest

# Add src to path
sys.path.insert(0, str(Path(__file__).parent.parent / "src"))


# =============================================================================
# Configuration
# =============================================================================

@dataclass
class TestConfig:
    """Test configuration."""
    # Test data directories
    temp_base: Path = Path(tempfile.gettempdir()) / "nexus_tests"
    test_drives: List[str] = None  # Will use temp dirs to simulate drives

    # Dummy file settings
    max_file_size_mb: int = 100
    max_files_per_dir: int = 100
    max_depth: int = 5

    # Performance thresholds
    index_speed_files_per_sec: int = 100000  # Minimum expected
    search_latency_ms: int = 10  # Maximum expected

    # Test modes
    use_real_drives: bool = False  # Set True to test on actual C: drive
    cleanup_after: bool = True

    def __post_init__(self):
        if self.test_drives is None:
            self.test_drives = ["C", "D", "E"]


@pytest.fixture(scope="session")
def test_config() -> TestConfig:
    """Global test configuration."""
    return TestConfig()


# =============================================================================
# Temp Directory Fixtures
# =============================================================================

@pytest.fixture(scope="session")
def test_root(test_config: TestConfig) -> Generator[Path, None, None]:
    """Create and cleanup test root directory."""
    root = test_config.temp_base / f"run_{datetime.now().strftime('%Y%m%d_%H%M%S')}"
    root.mkdir(parents=True, exist_ok=True)

    yield root

    if test_config.cleanup_after:
        shutil.rmtree(root, ignore_errors=True)


@pytest.fixture
def temp_dir(test_root: Path) -> Generator[Path, None, None]:
    """Create a temp directory for a single test."""
    test_dir = test_root / f"test_{random.randint(10000, 99999)}"
    test_dir.mkdir(parents=True, exist_ok=True)

    yield test_dir

    shutil.rmtree(test_dir, ignore_errors=True)


# =============================================================================
# Dummy File Generation
# =============================================================================

@dataclass
class DummyFileSpec:
    """Specification for dummy file generation."""
    name: str
    size_bytes: int
    extension: str = ".bin"
    content_pattern: str = "random"  # "random", "zeros", "text", "binary"
    age_days: int = 0  # How old the file should appear


class DummyFileGenerator:
    """
    Generate dummy files for testing storage features.

    Supports:
    - Various file sizes (bytes to GB)
    - Different file types and extensions
    - Simulated file ages
    - Duplicate detection testing
    - Directory structure generation
    """

    EXTENSIONS_BY_TYPE = {
        "model": [".gguf", ".safetensors", ".bin", ".pt", ".onnx"],
        "video": [".mp4", ".mkv", ".avi", ".mov"],
        "image": [".jpg", ".png", ".gif", ".bmp", ".webp"],
        "audio": [".mp3", ".wav", ".flac", ".m4a"],
        "document": [".pdf", ".doc", ".docx", ".txt", ".md"],
        "archive": [".zip", ".rar", ".7z", ".tar.gz"],
        "code": [".py", ".js", ".ts", ".rs", ".go", ".java", ".cs"],
        "data": [".json", ".xml", ".csv", ".yaml", ".sql"],
        "temp": [".tmp", ".temp", ".bak", ".log", ".cache"],
    }

    def __init__(self, base_dir: Path):
        self.base_dir = base_dir
        self.created_files: List[Path] = []
        self.created_dirs: List[Path] = []

    def generate_content(self, size: int, pattern: str = "random") -> bytes:
        """Generate file content of specified size and pattern."""
        if pattern == "zeros":
            return b"\x00" * size
        elif pattern == "text":
            # Repeating text pattern
            text = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. " * 100
            return (text * (size // len(text) + 1))[:size].encode()
        elif pattern == "binary":
            # Structured binary pattern (for testing hash detection)
            header = b"NEXUS_TEST_FILE\x00\x00\x00\x00"
            body_size = size - len(header)
            return header + bytes(range(256)) * (body_size // 256 + 1)[:body_size]
        else:  # random
            return random.randbytes(size)

    def create_file(
        self,
        name: str,
        size_bytes: int,
        subdir: Optional[str] = None,
        extension: Optional[str] = None,
        pattern: str = "random",
        age_days: int = 0,
    ) -> Path:
        """Create a single dummy file."""
        if subdir:
            dir_path = self.base_dir / subdir
            dir_path.mkdir(parents=True, exist_ok=True)
        else:
            dir_path = self.base_dir

        if extension and not name.endswith(extension):
            name = name + extension

        file_path = dir_path / name
        content = self.generate_content(size_bytes, pattern)
        file_path.write_bytes(content)

        # Set file age if specified
        if age_days > 0:
            old_time = (datetime.now() - timedelta(days=age_days)).timestamp()
            os.utime(file_path, (old_time, old_time))

        self.created_files.append(file_path)
        return file_path

    def create_directory_structure(
        self,
        structure: Dict[str, Any],
        parent: Optional[Path] = None,
    ) -> Path:
        """
        Create a directory structure from a dict specification.

        Example:
        {
            "projects": {
                "project1": {
                    "__files__": [
                        {"name": "readme.md", "size": 1024},
                        {"name": "main.py", "size": 2048},
                    ],
                    "src": {...}
                }
            }
        }
        """
        parent = parent or self.base_dir

        for name, content in structure.items():
            if name == "__files__":
                for file_spec in content:
                    self.create_file(
                        name=file_spec["name"],
                        size_bytes=file_spec.get("size", 1024),
                        subdir=str(parent.relative_to(self.base_dir)) if parent != self.base_dir else None,
                        pattern=file_spec.get("pattern", "random"),
                        age_days=file_spec.get("age_days", 0),
                    )
            else:
                dir_path = parent / name
                dir_path.mkdir(parents=True, exist_ok=True)
                self.created_dirs.append(dir_path)
                if isinstance(content, dict):
                    self.create_directory_structure(content, dir_path)

        return parent

    def create_large_files(
        self,
        count: int = 10,
        min_size_mb: float = 1.0,
        max_size_mb: float = 10.0,
        file_type: str = "model",
    ) -> List[Path]:
        """Create multiple large files for testing."""
        files = []
        extensions = self.EXTENSIONS_BY_TYPE.get(file_type, [".bin"])

        for i in range(count):
            size = int(random.uniform(min_size_mb, max_size_mb) * 1024 * 1024)
            ext = random.choice(extensions)
            name = f"large_file_{i:04d}{ext}"
            file_path = self.create_file(name, size, subdir="large_files")
            files.append(file_path)

        return files

    def create_duplicates(
        self,
        original_size: int = 1024 * 1024,
        num_copies: int = 3,
        spread_dirs: bool = True,
    ) -> List[Path]:
        """Create duplicate files for testing deduplication."""
        content = self.generate_content(original_size, "random")
        content_hash = hashlib.md5(content).hexdigest()[:8]

        files = []
        for i in range(num_copies):
            if spread_dirs:
                subdir = f"dup_dir_{i}"
            else:
                subdir = "duplicates"

            name = f"duplicate_{content_hash}_{i}.bin"
            file_path = self.base_dir / (subdir if subdir else "") / name
            file_path.parent.mkdir(parents=True, exist_ok=True)
            file_path.write_bytes(content)
            files.append(file_path)
            self.created_files.append(file_path)

        return files

    def create_mixed_test_set(
        self,
        small_files: int = 100,
        medium_files: int = 50,
        large_files: int = 10,
        temp_files: int = 30,
        duplicates: int = 5,
    ) -> Dict[str, List[Path]]:
        """Create a comprehensive test set with various file types."""
        result = {
            "small": [],
            "medium": [],
            "large": [],
            "temp": [],
            "duplicates": [],
        }

        # Small files (< 1KB)
        for i in range(small_files):
            ext = random.choice([".txt", ".json", ".md", ".py"])
            path = self.create_file(
                f"small_{i}{ext}",
                random.randint(100, 1024),
                subdir="small_files",
                pattern="text",
            )
            result["small"].append(path)

        # Medium files (1KB - 1MB)
        for i in range(medium_files):
            ext = random.choice([".pdf", ".doc", ".csv", ".xml"])
            path = self.create_file(
                f"medium_{i}{ext}",
                random.randint(1024, 1024 * 1024),
                subdir="medium_files",
                pattern="binary",
            )
            result["medium"].append(path)

        # Large files (1MB - 100MB)
        for i in range(large_files):
            ext = random.choice([".mp4", ".zip", ".gguf"])
            path = self.create_file(
                f"large_{i}{ext}",
                random.randint(1024 * 1024, 100 * 1024 * 1024),
                subdir="large_files",
                pattern="random",
            )
            result["large"].append(path)

        # Temp/cache files
        for i in range(temp_files):
            ext = random.choice([".tmp", ".cache", ".log", ".bak"])
            path = self.create_file(
                f"temp_{i}{ext}",
                random.randint(100, 10000),
                subdir="temp_cache",
                pattern="zeros",
                age_days=random.randint(1, 90),
            )
            result["temp"].append(path)

        # Duplicates
        for i in range(duplicates):
            dups = self.create_duplicates(
                original_size=random.randint(10000, 100000),
                num_copies=random.randint(2, 4),
                spread_dirs=True,
            )
            result["duplicates"].extend(dups)

        return result

    def cleanup(self) -> None:
        """Clean up all created files and directories."""
        for f in self.created_files:
            try:
                f.unlink()
            except Exception:
                pass

        for d in sorted(self.created_dirs, reverse=True):
            try:
                d.rmdir()
            except Exception:
                pass

        self.created_files.clear()
        self.created_dirs.clear()

    def get_stats(self) -> Dict[str, Any]:
        """Get statistics about created test files."""
        total_size = sum(f.stat().st_size for f in self.created_files if f.exists())
        return {
            "file_count": len(self.created_files),
            "dir_count": len(self.created_dirs),
            "total_size_bytes": total_size,
            "total_size_mb": total_size / (1024 * 1024),
        }


@pytest.fixture
def file_generator(temp_dir: Path) -> Generator[DummyFileGenerator, None, None]:
    """Provide a dummy file generator for a test."""
    generator = DummyFileGenerator(temp_dir)

    yield generator

    generator.cleanup()


@pytest.fixture
def small_test_set(file_generator: DummyFileGenerator) -> Dict[str, List[Path]]:
    """Create a small test set (fast tests)."""
    return file_generator.create_mixed_test_set(
        small_files=10,
        medium_files=5,
        large_files=1,
        temp_files=5,
        duplicates=2,
    )


@pytest.fixture
def large_test_set(file_generator: DummyFileGenerator) -> Dict[str, List[Path]]:
    """Create a large test set (comprehensive tests)."""
    return file_generator.create_mixed_test_set(
        small_files=1000,
        medium_files=100,
        large_files=10,
        temp_files=50,
        duplicates=10,
    )


# =============================================================================
# Mock AI Provider
# =============================================================================

class MockAIProvider:
    """Mock AI provider for testing without API calls."""

    def __init__(self, responses: Optional[Dict[str, str]] = None):
        self.responses = responses or {}
        self.calls: List[Dict[str, Any]] = []

    async def chat(self, messages: List[Any], **kwargs) -> Any:
        """Mock chat completion."""
        from nexus_ai.core.ai_providers import AIResponse, ProviderType

        prompt = messages[-1].content if messages else ""
        self.calls.append({"messages": messages, "kwargs": kwargs})

        # Check for predefined response
        for key, response in self.responses.items():
            if key.lower() in prompt.lower():
                return AIResponse(
                    content=response,
                    model="mock-model",
                    provider=ProviderType.LMSTUDIO,
                )

        # Default response
        return AIResponse(
            content='{"suggestions": [], "message": "Mock response"}',
            model="mock-model",
            provider=ProviderType.LMSTUDIO,
        )

    async def stream_chat(self, messages: List[Any], **kwargs):
        """Mock streaming chat."""
        response = await self.chat(messages, **kwargs)
        for word in response.content.split():
            yield word + " "


@pytest.fixture
def mock_ai_provider() -> MockAIProvider:
    """Provide a mock AI provider."""
    return MockAIProvider()


# =============================================================================
# Async Event Loop
# =============================================================================

@pytest.fixture(scope="session")
def event_loop():
    """Create an event loop for the test session."""
    loop = asyncio.new_event_loop()
    yield loop
    loop.close()


# =============================================================================
# Performance Markers
# =============================================================================

@pytest.fixture
def benchmark_threshold(test_config: TestConfig) -> Dict[str, float]:
    """Performance thresholds for benchmarks."""
    return {
        "index_files_per_sec": test_config.index_speed_files_per_sec,
        "search_latency_ms": test_config.search_latency_ms,
        "space_analysis_sec": 5.0,
        "file_hash_mb_per_sec": 100.0,
    }


# =============================================================================
# Skip Markers
# =============================================================================

def pytest_configure(config):
    """Configure custom markers."""
    config.addinivalue_line("markers", "slow: mark test as slow running")
    config.addinivalue_line("markers", "gpu: mark test as requiring GPU")
    config.addinivalue_line("markers", "integration: mark test as integration test")
    config.addinivalue_line("markers", "requires_admin: mark test as requiring admin rights")
    config.addinivalue_line("markers", "benchmark: mark test as performance benchmark")


# Skip conditions
skip_slow = pytest.mark.skipif(
    os.getenv("SKIP_SLOW_TESTS", "0") == "1",
    reason="Slow tests skipped via SKIP_SLOW_TESTS env var"
)

skip_gpu = pytest.mark.skipif(
    os.getenv("CUDA_VISIBLE_DEVICES", "") == "",
    reason="GPU tests skipped - no GPU available"
)

skip_integration = pytest.mark.skipif(
    os.getenv("SKIP_INTEGRATION_TESTS", "0") == "1",
    reason="Integration tests skipped via env var"
)
