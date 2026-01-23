"""
Space Analyzer - Hyper-Threaded Disk Space Analysis

Features:
- Multi-threaded directory scanning (faster than Everything Search)
- Large file detection with categorization
- Duplicate file finding using content hashing
- Cache/temp file identification
- Model directory tracking
- Space usage visualization
"""

from __future__ import annotations

import hashlib
import os
import threading
import time
from collections import defaultdict
from collections.abc import Callable
from concurrent.futures import ThreadPoolExecutor, as_completed
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path

from loguru import logger

# Try to import optional dependencies
RICH_AVAILABLE = False
try:
    from rich.console import Console
    from rich.table import Table
    RICH_AVAILABLE = True
except ImportError:
    pass


@dataclass
class FileInfo:
    """Information about a single file."""
    path: str
    name: str
    size: int
    modified: datetime
    extension: str
    is_hidden: bool = False
    content_hash: str | None = None
    category: str = "other"


@dataclass
class DirectoryInfo:
    """Information about a directory."""
    path: str
    name: str
    total_size: int = 0
    file_count: int = 0
    dir_count: int = 0
    largest_file: FileInfo | None = None
    modified: datetime | None = None


@dataclass
class SpaceAnalysis:
    """Complete space analysis results."""
    drive: str
    total_size: int = 0
    free_space: int = 0
    used_space: int = 0
    file_count: int = 0
    dir_count: int = 0
    scan_time_ms: int = 0

    # Categorized files
    large_files: list[FileInfo] = field(default_factory=list)
    huge_files: list[FileInfo] = field(default_factory=list)
    model_files: list[FileInfo] = field(default_factory=list)
    cache_files: list[FileInfo] = field(default_factory=list)
    temp_files: list[FileInfo] = field(default_factory=list)
    duplicate_groups: dict[str, list[FileInfo]] = field(default_factory=dict)

    # Top consumers
    largest_dirs: list[DirectoryInfo] = field(default_factory=list)
    by_extension: dict[str, int] = field(default_factory=dict)
    by_category: dict[str, int] = field(default_factory=dict)


class SpaceAnalyzer:
    """
    Hyper-threaded disk space analyzer.

    Uses aggressive multi-threading to scan directories faster than
    traditional tools like Everything Search.
    """

    # File categories and their extensions
    CATEGORIES = {
        "model": {".gguf", ".safetensors", ".bin", ".pt", ".pth", ".onnx", ".h5", ".pb", ".tflite", ".mlmodel"},
        "video": {".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v"},
        "image": {".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp", ".svg", ".psd", ".raw"},
        "audio": {".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a"},
        "archive": {".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz"},
        "document": {".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".odt"},
        "code": {".py", ".js", ".ts", ".rs", ".go", ".java", ".c", ".cpp", ".h", ".cs"},
        "data": {".json", ".xml", ".yaml", ".yml", ".csv", ".sql", ".db", ".sqlite"},
        "cache": {".cache", ".tmp", ".temp", ".log", ".bak"},
    }

    # Known model directories
    MODEL_DIRS = {
        ".lmstudio",
        ".ollama",
        ".cache/huggingface",
        ".cache/torch",
        "models",
        "lora",
        "checkpoints",
        "stable-diffusion",
        "ComfyUI/models",
    }

    # Temp/cache patterns
    TEMP_PATTERNS = {
        "temp", "tmp", "cache", ".cache", "__pycache__",
        "node_modules", ".npm", ".yarn", ".pnpm",
        "venv", ".venv", "env", ".env",
        ".git/objects",
    }

    def __init__(
        self,
        large_threshold_gb: float = 1.0,
        huge_threshold_gb: float = 10.0,
        max_workers: int | None = None,
        compute_hashes: bool = False,
        progress_callback: Callable[[str, int], None] | None = None,
    ):
        """
        Initialize the space analyzer.

        Args:
            large_threshold_gb: Size threshold for "large" files (GB)
            huge_threshold_gb: Size threshold for "huge" files (GB)
            max_workers: Max thread workers (default: CPU count * 4)
            compute_hashes: Whether to compute content hashes for dedup
            progress_callback: Optional callback(path, count) for progress
        """
        self.large_threshold = int(large_threshold_gb * 1024**3)
        self.huge_threshold = int(huge_threshold_gb * 1024**3)
        self.max_workers = max_workers or (os.cpu_count() or 4) * 4
        self.compute_hashes = compute_hashes
        self.progress_callback = progress_callback

        # Thread-safe counters
        self._file_count = 0
        self._dir_count = 0
        self._total_size = 0
        self._lock = threading.Lock()

        # Results
        self._files: list[FileInfo] = []
        self._dirs: dict[str, DirectoryInfo] = {}

        logger.info(f"SpaceAnalyzer initialized with {self.max_workers} workers")

    def _get_category(self, ext: str) -> str:
        """Get file category from extension."""
        ext = ext.lower()
        for category, extensions in self.CATEGORIES.items():
            if ext in extensions:
                return category
        return "other"

    def _is_model_path(self, path: str) -> bool:
        """Check if path is in a model directory."""
        path_lower = path.lower()
        return any(model_dir in path_lower for model_dir in self.MODEL_DIRS)

    def _is_temp_path(self, path: str) -> bool:
        """Check if path is a temp/cache directory."""
        path_lower = path.lower()
        return any(pattern in path_lower for pattern in self.TEMP_PATTERNS)

    def _compute_quick_hash(self, path: Path) -> str | None:
        """Compute a quick hash (first 64KB + size)."""
        try:
            with open(path, "rb") as f:
                data = f.read(65536)
            size = path.stat().st_size
            return hashlib.md5(data + str(size).encode()).hexdigest()
        except Exception:
            return None

    def _scan_file(self, path: Path) -> FileInfo | None:
        """Scan a single file and return its info."""
        try:
            stat = path.stat()
            ext = path.suffix.lower()

            info = FileInfo(
                path=str(path),
                name=path.name,
                size=stat.st_size,
                modified=datetime.fromtimestamp(stat.st_mtime),
                extension=ext,
                is_hidden=path.name.startswith("."),
                category=self._get_category(ext),
            )

            # Compute hash if enabled and file is large enough
            if self.compute_hashes and stat.st_size > 1024 * 1024:  # > 1MB
                info.content_hash = self._compute_quick_hash(path)

            return info

        except (PermissionError, OSError) as e:
            logger.debug(f"Cannot access file {path}: {e}")
            return None

    def _scan_directory_shallow(self, path: Path) -> tuple[list[Path], list[Path]]:
        """
        Shallow scan a directory (one level only).

        Returns: (files, subdirs)
        """
        files = []
        subdirs = []

        try:
            with os.scandir(path) as entries:
                for entry in entries:
                    try:
                        if entry.is_file(follow_symlinks=False):
                            files.append(Path(entry.path))
                        elif entry.is_dir(follow_symlinks=False):
                            subdirs.append(Path(entry.path))
                    except (PermissionError, OSError):
                        pass
        except (PermissionError, OSError) as e:
            logger.debug(f"Cannot scan directory {path}: {e}")

        return files, subdirs

    def _process_directory(
        self,
        path: Path,
        executor: ThreadPoolExecutor,
        depth: int = 0,
        max_depth: int = 100,
    ) -> DirectoryInfo:
        """
        Recursively process a directory with parallel file scanning.

        Uses a hybrid approach:
        - Shallow scan to get immediate children
        - Parallel file processing
        - Recursive directory processing with work stealing
        """
        dir_info = DirectoryInfo(
            path=str(path),
            name=path.name,
        )

        if depth >= max_depth:
            return dir_info

        files, subdirs = self._scan_directory_shallow(path)

        # Process files in parallel
        file_futures = {executor.submit(self._scan_file, f): f for f in files}

        for future in as_completed(file_futures):
            file_info = future.result()
            if file_info:
                dir_info.total_size += file_info.size
                dir_info.file_count += 1

                with self._lock:
                    self._files.append(file_info)
                    self._file_count += 1
                    self._total_size += file_info.size

                    if self.progress_callback and self._file_count % 1000 == 0:
                        self.progress_callback(str(path), self._file_count)

                # Track largest file
                if dir_info.largest_file is None or file_info.size > dir_info.largest_file.size:
                    dir_info.largest_file = file_info

        # Process subdirectories recursively
        for subdir in subdirs:
            subdir_info = self._process_directory(subdir, executor, depth + 1, max_depth)
            dir_info.total_size += subdir_info.total_size
            dir_info.file_count += subdir_info.file_count
            dir_info.dir_count += subdir_info.dir_count + 1

            with self._lock:
                self._dir_count += 1
                self._dirs[str(subdir)] = subdir_info

        return dir_info

    def analyze_path(self, path: Path | str, max_depth: int = 100) -> SpaceAnalysis:
        """
        Analyze disk space usage for a path.

        Args:
            path: Directory path to analyze
            max_depth: Maximum recursion depth

        Returns:
            SpaceAnalysis with detailed results
        """
        path = Path(path)
        if not path.exists():
            raise ValueError(f"Path does not exist: {path}")

        start_time = time.time()

        # Reset counters
        self._file_count = 0
        self._dir_count = 0
        self._total_size = 0
        self._files = []
        self._dirs = {}

        logger.info(f"Starting analysis of {path} with {self.max_workers} workers")

        # Get drive info
        try:
            import shutil
            total, used, free = shutil.disk_usage(path)
        except Exception:
            total, used, free = 0, 0, 0

        # Process with thread pool
        with ThreadPoolExecutor(max_workers=self.max_workers) as executor:
            root_info = self._process_directory(path, executor, max_depth=max_depth)
            self._dirs[str(path)] = root_info

        scan_time_ms = int((time.time() - start_time) * 1000)

        # Build analysis results
        analysis = SpaceAnalysis(
            drive=str(path)[:2] if len(str(path)) >= 2 else str(path),
            total_size=total,
            free_space=free,
            used_space=used,
            file_count=self._file_count,
            dir_count=self._dir_count,
            scan_time_ms=scan_time_ms,
        )

        # Categorize files
        hash_groups: dict[str, list[FileInfo]] = defaultdict(list)

        for f in self._files:
            # Size categories
            if f.size >= self.huge_threshold:
                analysis.huge_files.append(f)
            elif f.size >= self.large_threshold:
                analysis.large_files.append(f)

            # Type categories
            if self._is_model_path(f.path) or f.category == "model":
                analysis.model_files.append(f)
            elif self._is_temp_path(f.path) or f.category == "cache":
                if "cache" in f.path.lower():
                    analysis.cache_files.append(f)
                else:
                    analysis.temp_files.append(f)

            # Extension stats
            ext = f.extension or "(no extension)"
            analysis.by_extension[ext] = analysis.by_extension.get(ext, 0) + f.size

            # Category stats
            analysis.by_category[f.category] = analysis.by_category.get(f.category, 0) + f.size

            # Hash groups for duplicates
            if f.content_hash:
                hash_groups[f.content_hash].append(f)

        # Find duplicates (groups with >1 file)
        for hash_val, files in hash_groups.items():
            if len(files) > 1:
                analysis.duplicate_groups[hash_val] = sorted(files, key=lambda x: -x.size)

        # Sort results
        analysis.large_files.sort(key=lambda x: -x.size)
        analysis.huge_files.sort(key=lambda x: -x.size)
        analysis.model_files.sort(key=lambda x: -x.size)

        # Top directories
        analysis.largest_dirs = sorted(
            self._dirs.values(),
            key=lambda x: -x.total_size
        )[:50]

        logger.info(
            f"Analysis complete: {self._file_count:,} files, "
            f"{self._dir_count:,} dirs, {self._total_size / 1024**3:.2f} GB "
            f"in {scan_time_ms}ms"
        )

        return analysis

    def analyze_drive(self, drive_letter: str, max_depth: int = 100) -> SpaceAnalysis:
        """Analyze an entire drive."""
        path = Path(f"{drive_letter.upper()}:\\")
        return self.analyze_path(path, max_depth)

    def find_large_files(
        self,
        path: Path | str,
        min_size_gb: float = 1.0,
        limit: int = 100,
    ) -> list[FileInfo]:
        """
        Quick scan to find large files only.

        Optimized for speed when you just need large files.
        """
        path = Path(path)
        min_size = int(min_size_gb * 1024**3)
        large_files: list[FileInfo] = []

        def scan_dir(dir_path: Path) -> None:
            try:
                with os.scandir(dir_path) as entries:
                    for entry in entries:
                        try:
                            if entry.is_file(follow_symlinks=False):
                                stat = entry.stat()
                                if stat.st_size >= min_size:
                                    large_files.append(FileInfo(
                                        path=entry.path,
                                        name=entry.name,
                                        size=stat.st_size,
                                        modified=datetime.fromtimestamp(stat.st_mtime),
                                        extension=Path(entry.name).suffix.lower(),
                                    ))
                            elif entry.is_dir(follow_symlinks=False):
                                scan_dir(Path(entry.path))
                        except (PermissionError, OSError):
                            pass
            except (PermissionError, OSError):
                pass

        # First scan files in root directory
        try:
            with os.scandir(path) as entries:
                for entry in entries:
                    try:
                        if entry.is_file(follow_symlinks=False):
                            stat = entry.stat()
                            if stat.st_size >= min_size:
                                large_files.append(FileInfo(
                                    path=entry.path,
                                    name=entry.name,
                                    size=stat.st_size,
                                    modified=datetime.fromtimestamp(stat.st_mtime),
                                    extension=Path(entry.name).suffix.lower(),
                                ))
                    except (PermissionError, OSError):
                        pass
        except (PermissionError, OSError):
            pass

        with ThreadPoolExecutor(max_workers=self.max_workers) as executor:
            # Get top-level directories
            try:
                top_dirs = [d for d in path.iterdir() if d.is_dir()]
            except PermissionError:
                top_dirs = []

            # Scan subdirectories in parallel
            futures = [executor.submit(scan_dir, d) for d in top_dirs]
            for _ in as_completed(futures):
                pass  # Just wait for completion

        large_files.sort(key=lambda x: -x.size)
        return large_files[:limit]

    def format_size(self, size: int) -> str:
        """Format size in human-readable format."""
        size_f: float = float(size)
        for unit in ["B", "KB", "MB", "GB", "TB"]:
            if size_f < 1024:
                return f"{size_f:.2f} {unit}"
            size_f /= 1024
        return f"{size_f:.2f} PB"

    def print_report(self, analysis: SpaceAnalysis) -> None:
        """Print a formatted report of the analysis."""
        if RICH_AVAILABLE:
            self._print_rich_report(analysis)
        else:
            self._print_simple_report(analysis)

    def _print_simple_report(self, analysis: SpaceAnalysis) -> None:
        """Print simple text report."""
        print(f"\n{'='*60}")
        print(f"  NexusFS Space Analysis: {analysis.drive}")
        print(f"{'='*60}")
        print(f"\nDrive: {self.format_size(analysis.used_space)} used / "
              f"{self.format_size(analysis.total_size)} total "
              f"({self.format_size(analysis.free_space)} free)")
        print(f"Scanned: {analysis.file_count:,} files, {analysis.dir_count:,} directories")
        print(f"Scan time: {analysis.scan_time_ms}ms")

        print(f"\n--- HUGE FILES (>{self.huge_threshold / 1024**3:.0f} GB) ---")
        for f in analysis.huge_files[:10]:
            print(f"  {self.format_size(f.size):>12}  {f.path}")

        print(f"\n--- LARGE FILES (>{self.large_threshold / 1024**3:.0f} GB) ---")
        for f in analysis.large_files[:10]:
            print(f"  {self.format_size(f.size):>12}  {f.path}")

        print("\n--- MODEL FILES ---")
        total_models = sum(f.size for f in analysis.model_files)
        print(f"Total model storage: {self.format_size(total_models)}")
        for f in analysis.model_files[:10]:
            print(f"  {self.format_size(f.size):>12}  {f.path}")

        print("\n--- LARGEST DIRECTORIES ---")
        for d in analysis.largest_dirs[:10]:
            print(f"  {self.format_size(d.total_size):>12}  {d.path}")

    def _print_rich_report(self, analysis: SpaceAnalysis) -> None:
        """Print rich formatted report."""
        console = Console()

        console.print(f"\n[bold cyan]{'='*60}[/bold cyan]")
        console.print(f"[bold cyan]  NexusFS Space Analysis: {analysis.drive}[/bold cyan]")
        console.print(f"[bold cyan]{'='*60}[/bold cyan]")

        # Drive summary
        console.print(f"\n[yellow]Drive:[/yellow] {self.format_size(analysis.used_space)} used / "
                      f"{self.format_size(analysis.total_size)} total "
                      f"([green]{self.format_size(analysis.free_space)} free[/green])")
        console.print(f"[yellow]Scanned:[/yellow] {analysis.file_count:,} files, "
                      f"{analysis.dir_count:,} directories in {analysis.scan_time_ms}ms")

        # Huge files table
        if analysis.huge_files:
            table = Table(title=f"Huge Files (>{self.huge_threshold / 1024**3:.0f} GB)")
            table.add_column("Size", style="red", width=12)
            table.add_column("Path", style="white")
            for f in analysis.huge_files[:10]:
                table.add_row(self.format_size(f.size), f.path)
            console.print(table)

        # Large files table
        if analysis.large_files:
            table = Table(title=f"Large Files (>{self.large_threshold / 1024**3:.0f} GB)")
            table.add_column("Size", style="yellow", width=12)
            table.add_column("Path", style="white")
            for f in analysis.large_files[:10]:
                table.add_row(self.format_size(f.size), f.path)
            console.print(table)

        # Model files
        if analysis.model_files:
            total_models = sum(f.size for f in analysis.model_files)
            table = Table(title=f"Model Files (Total: {self.format_size(total_models)})")
            table.add_column("Size", style="magenta", width=12)
            table.add_column("Path", style="white")
            for f in analysis.model_files[:10]:
                table.add_row(self.format_size(f.size), f.path)
            console.print(table)

        # Largest directories
        table = Table(title="Largest Directories")
        table.add_column("Size", style="cyan", width=12)
        table.add_column("Files", width=10)
        table.add_column("Path", style="white")
        for d in analysis.largest_dirs[:10]:
            table.add_row(
                self.format_size(d.total_size),
                str(d.file_count),
                d.path
            )
        console.print(table)


def main():
    """CLI entry point for space analyzer."""
    import sys

    path = sys.argv[1] if len(sys.argv) > 1 else "C:\\Users\\Admin"

    analyzer = SpaceAnalyzer(
        large_threshold_gb=1.0,
        huge_threshold_gb=10.0,
        compute_hashes=False,
    )

    print(f"Analyzing {path}...")
    analysis = analyzer.analyze_path(path)
    analyzer.print_report(analysis)


if __name__ == "__main__":
    main()
