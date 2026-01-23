"""
NexusFS Configuration Management

Central configuration for all NexusFS components.
"""

import os
from pathlib import Path

import toml
from pydantic import BaseModel, Field
from pydantic_settings import BaseSettings

# Default paths
DEFAULT_DATA_DIR = Path("D:/NexusFS/data")
DEFAULT_CONFIG_DIR = Path("D:/NexusFS/configs")


class DriveConfig(BaseModel):
    """Configuration for a single drive."""
    letter: str
    enabled: bool = True
    priority: int = 0  # Higher = process first
    purpose: str = ""  # e.g., "models", "archive", "projects"
    min_free_gb: float = 50.0  # Alert if below this


class IndexConfig(BaseModel):
    """Indexing configuration."""
    # Drives to index
    drives: list[str] = Field(default_factory=lambda: ["C", "D", "E", "F", "G"])

    # Performance
    threads: int = Field(default_factory=lambda: os.cpu_count() or 8)
    thread_multiplier: float = 2.0  # Use threads * multiplier for I/O bound ops
    batch_size: int = 10000
    use_mft: bool = True  # Use MFT reader for NTFS (faster)
    use_usn_journal: bool = True  # Real-time monitoring

    # What to index
    include_hidden: bool = True
    include_system: bool = False
    compute_hashes: bool = False  # For deduplication
    max_hash_size_mb: int = 100
    deep_index: bool = False  # Include content embeddings

    # Exclusions
    exclude_dirs: list[str] = Field(default_factory=lambda: [
        "$Recycle.Bin",
        "System Volume Information",
        "Windows",
        "Program Files",
        "Program Files (x86)",
        "ProgramData",
        "Recovery",
        "PerfLogs",
    ])

    exclude_extensions: list[str] = Field(default_factory=lambda: [
        "tmp", "temp", "log", "bak",
    ])


class SearchConfig(BaseModel):
    """Search configuration."""
    # Performance
    max_results: int = 1000
    fuzzy_distance: int = 2
    snippet_length: int = 200

    # Index locations
    tantivy_index_path: Path = DEFAULT_DATA_DIR / "indices" / "tantivy"
    vector_index_path: Path = DEFAULT_DATA_DIR / "vectors"

    # Embedding model
    embedding_model: str = "all-MiniLM-L6-v2"
    embedding_batch_size: int = 32


class SpaceConfig(BaseModel):
    """Space management configuration."""
    # Large file thresholds
    large_file_gb: float = 1.0
    huge_file_gb: float = 10.0

    # Model directories to track
    model_dirs: list[str] = Field(default_factory=lambda: [
        ".lmstudio",
        ".ollama",
        ".cache/huggingface",
        ".cache/torch",
        "models",
    ])

    # Target drives for large files (by priority)
    large_file_targets: list[str] = Field(default_factory=lambda: ["D", "F", "G"])

    # Cleanup settings
    temp_max_age_days: int = 7
    cache_max_age_days: int = 30
    log_max_age_days: int = 14


class TransactionConfig(BaseModel):
    """Transaction and rollback configuration."""
    log_path: Path = DEFAULT_DATA_DIR / "transactions" / "transaction_log.jsonl"
    snapshot_dir: Path = DEFAULT_DATA_DIR / "transactions" / "snapshots"
    backup_dir: Path = DEFAULT_DATA_DIR / "backups"

    # Backup settings
    backup_enabled: bool = True
    backup_large_files: bool = False  # Full backup for files > large_file_gb
    backup_retention_days: int = 7
    max_backup_size_gb: float = 100.0

    # Snapshot settings
    snapshot_interval_hours: int = 24
    max_snapshots: int = 7


class NexusConfig(BaseSettings):
    """Main NexusFS configuration."""

    # Paths
    data_dir: Path = DEFAULT_DATA_DIR
    config_dir: Path = DEFAULT_CONFIG_DIR

    # Sub-configs
    index: IndexConfig = Field(default_factory=IndexConfig)
    search: SearchConfig = Field(default_factory=SearchConfig)
    space: SpaceConfig = Field(default_factory=SpaceConfig)
    transaction: TransactionConfig = Field(default_factory=TransactionConfig)

    # Drives
    drives: dict[str, DriveConfig] = Field(default_factory=lambda: {
        "C": DriveConfig(letter="C", priority=1, purpose="system", min_free_gb=100),
        "D": DriveConfig(letter="D", priority=2, purpose="projects", min_free_gb=100),
        "E": DriveConfig(letter="E", priority=3, purpose="archive", min_free_gb=50),
        "F": DriveConfig(letter="F", priority=4, purpose="ai_models", min_free_gb=100),
        "G": DriveConfig(letter="G", priority=5, purpose="large_files", min_free_gb=50),
    })

    # Performance
    max_workers: int = Field(default_factory=lambda: (os.cpu_count() or 8) * 2)
    io_workers: int = Field(default_factory=lambda: (os.cpu_count() or 8) * 4)

    # Logging
    log_level: str = "INFO"
    log_file: Path | None = DEFAULT_DATA_DIR / "logs" / "nexus.log"
    log_rotation: str = "10 MB"
    log_retention: int = 5

    class Config:
        env_prefix = "NEXUS_"
        env_file = ".env"

    @classmethod
    def load(cls, config_path: Path | None = None) -> "NexusConfig":
        """Load configuration from file."""
        if config_path is None:
            config_path = DEFAULT_CONFIG_DIR / "default.toml"

        if config_path.exists():
            data = toml.load(config_path)
            return cls(**data)

        return cls()

    def save(self, config_path: Path | None = None) -> None:
        """Save configuration to file."""
        if config_path is None:
            config_path = DEFAULT_CONFIG_DIR / "default.toml"

        config_path.parent.mkdir(parents=True, exist_ok=True)
        with open(config_path, "w") as f:
            toml.dump(self.model_dump(), f)


# Global config instance
_config: NexusConfig | None = None


def get_config() -> NexusConfig:
    """Get the global configuration instance."""
    global _config
    if _config is None:
        _config = NexusConfig.load()
    return _config


def set_config(config: NexusConfig) -> None:
    """Set the global configuration instance."""
    global _config
    _config = config
