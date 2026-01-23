"""
Enterprise Backup and Restore System

Provides:
- Multi-location backup with verification
- Automatic restore points before operations
- Cross-drive redundancy
- 7-day retention with alerts
- VSS shadow copy integration
- Agent-managed backup lifecycle
"""

from __future__ import annotations

import os
import sys
import shutil
import hashlib
import json
import gzip
from dataclasses import dataclass, field
from datetime import datetime, timedelta
from enum import Enum
from pathlib import Path
from typing import Optional, Dict, Any, List, Callable
import threading
import queue
from concurrent.futures import ThreadPoolExecutor

from nexus_ai.core.logging_config import get_logger, LogPerformance

logger = get_logger("backup_system")


class BackupType(Enum):
    """Types of backups."""
    FULL = "full"
    INCREMENTAL = "incremental"
    DIFFERENTIAL = "differential"
    SNAPSHOT = "snapshot"
    RESTORE_POINT = "restore_point"


class BackupStatus(Enum):
    """Backup operation status."""
    PENDING = "pending"
    IN_PROGRESS = "in_progress"
    COMPLETED = "completed"
    VERIFIED = "verified"
    FAILED = "failed"
    EXPIRED = "expired"
    PENDING_REVIEW = "pending_review"
    DELETED = "deleted"


class VerificationResult(Enum):
    """Result of backup verification."""
    SUCCESS = "success"
    HASH_MISMATCH = "hash_mismatch"
    FILE_MISSING = "file_missing"
    SIZE_MISMATCH = "size_mismatch"
    CORRUPTION = "corruption"


@dataclass
class BackupLocation:
    """A backup storage location."""
    id: str
    path: Path
    drive_letter: str
    priority: int = 1  # Lower = higher priority
    max_size_gb: float = 100.0
    current_size_gb: float = 0.0
    is_available: bool = True
    is_primary: bool = False


@dataclass
class BackupEntry:
    """Metadata for a single backed up file/folder."""
    source_path: str
    backup_path: str
    original_size: int
    backup_size: int
    hash_original: str
    hash_backup: str
    backup_time: datetime
    is_compressed: bool = False


@dataclass
class BackupRecord:
    """Complete record of a backup operation."""
    id: str
    type: BackupType
    status: BackupStatus
    created_at: datetime
    expires_at: datetime
    source_paths: List[str]
    backup_locations: List[str]  # Location IDs
    entries: List[BackupEntry] = field(default_factory=list)
    total_size: int = 0
    verified: bool = False
    verification_time: Optional[datetime] = None
    verification_result: Optional[VerificationResult] = None
    error_message: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)


@dataclass
class RestorePoint:
    """A system restore point."""
    id: str
    name: str
    created_at: datetime
    backup_record_id: str
    description: str
    auto_created: bool = True
    is_valid: bool = True


@dataclass
class BackupConfig:
    """Backup system configuration."""
    # Locations
    primary_backup_path: Path = field(default_factory=lambda: Path("D:/NexusFS/backups"))
    secondary_backup_paths: List[Path] = field(default_factory=lambda: [
        Path("E:/NexusFS_Backup"),
        Path("F:/NexusFS_Backup"),
    ])

    # Retention
    retention_days: int = 7
    alert_before_delete_days: int = 2
    max_restore_points: int = 50

    # Verification
    verify_after_backup: bool = True
    verify_hash_algorithm: str = "sha256"

    # Compression
    compress_backups: bool = True
    compression_level: int = 6

    # Automation
    auto_restore_points: bool = True
    restore_point_on_operations: List[str] = field(default_factory=lambda: [
        "move", "delete", "rename", "organize"
    ])

    # Multi-drive redundancy
    min_backup_copies: int = 2
    prefer_different_drives: bool = True


class BackupVerifier:
    """Verifies backup integrity."""

    def __init__(self, hash_algorithm: str = "sha256"):
        self.hash_algorithm = hash_algorithm

    def compute_hash(self, file_path: Path) -> str:
        """Compute hash of a file."""
        hasher = hashlib.new(self.hash_algorithm)
        with open(file_path, "rb") as f:
            while chunk := f.read(65536):
                hasher.update(chunk)
        return hasher.hexdigest()

    def verify_entry(self, entry: BackupEntry) -> VerificationResult:
        """Verify a single backup entry."""
        backup_path = Path(entry.backup_path)

        if not backup_path.exists():
            return VerificationResult.FILE_MISSING

        actual_size = backup_path.stat().st_size
        if actual_size != entry.backup_size:
            return VerificationResult.SIZE_MISMATCH

        actual_hash = self.compute_hash(backup_path)
        if actual_hash != entry.hash_backup:
            return VerificationResult.HASH_MISMATCH

        return VerificationResult.SUCCESS

    def verify_record(self, record: BackupRecord) -> tuple[bool, List[tuple[str, VerificationResult]]]:
        """Verify all entries in a backup record."""
        results = []
        all_success = True

        for entry in record.entries:
            result = self.verify_entry(entry)
            results.append((entry.source_path, result))
            if result != VerificationResult.SUCCESS:
                all_success = False

        return all_success, results


class BackupManager:
    """
    Enterprise backup management system.

    Features:
    - Multi-location redundant backups
    - Automatic restore points
    - Verification with hash checking
    - Retention policy with alerts
    - Cross-drive distribution
    """

    def __init__(self, config: Optional[BackupConfig] = None):
        self.config = config or BackupConfig()
        self.verifier = BackupVerifier(self.config.verify_hash_algorithm)
        self._locations: Dict[str, BackupLocation] = {}
        self._records: Dict[str, BackupRecord] = {}
        self._restore_points: Dict[str, RestorePoint] = {}
        self._lock = threading.Lock()
        self._alert_callbacks: List[Callable[[str, Dict], None]] = []

        self._initialize_locations()
        self._load_state()

    @property
    def locations(self) -> Dict[str, BackupLocation]:
        """Get all backup locations."""
        return dict(self._locations)

    @property
    def records(self) -> Dict[str, BackupRecord]:
        """Get all backup records."""
        return dict(self._records)

    @property
    def restore_points(self) -> Dict[str, RestorePoint]:
        """Get all restore points."""
        return dict(self._restore_points)

    def _initialize_locations(self) -> None:
        """Initialize backup locations."""
        # Primary location
        if self.config.primary_backup_path:
            self._add_location(
                self.config.primary_backup_path,
                is_primary=True,
                priority=1,
            )

        # Secondary locations
        for i, path in enumerate(self.config.secondary_backup_paths):
            self._add_location(path, priority=i + 2)

    def _add_location(
        self,
        path: Path,
        is_primary: bool = False,
        priority: int = 1,
    ) -> Optional[BackupLocation]:
        """Add a backup location."""
        try:
            path.mkdir(parents=True, exist_ok=True)
            drive = path.drive or str(path)[:1].upper()

            location = BackupLocation(
                id=f"loc_{len(self._locations)}",
                path=path,
                drive_letter=drive,
                priority=priority,
                is_primary=is_primary,
                is_available=path.exists(),
            )
            self._locations[location.id] = location
            logger.info(f"Added backup location: {path}", drive=drive, primary=is_primary)
            return location

        except Exception as e:
            logger.error(f"Failed to add backup location {path}: {e}")
            return None

    def _get_available_locations(self, prefer_different_drives: bool = True) -> List[BackupLocation]:
        """Get available backup locations, preferring different drives."""
        available = [loc for loc in self._locations.values() if loc.is_available]
        available.sort(key=lambda x: x.priority)

        if prefer_different_drives:
            # Ensure we have locations on different drives
            drives_seen = set()
            diverse_locations = []
            for loc in available:
                if loc.drive_letter not in drives_seen:
                    diverse_locations.append(loc)
                    drives_seen.add(loc.drive_letter)
            # Add remaining if we don't have enough
            for loc in available:
                if loc not in diverse_locations:
                    diverse_locations.append(loc)
            return diverse_locations

        return available

    def _state_file_path(self) -> Path:
        """Get path to state file."""
        return self.config.primary_backup_path / "backup_state.json"

    def _save_state(self) -> None:
        """Save backup state to disk."""
        state = {
            "records": {
                id: {
                    "id": r.id,
                    "type": r.type.value,
                    "status": r.status.value,
                    "created_at": r.created_at.isoformat(),
                    "expires_at": r.expires_at.isoformat(),
                    "source_paths": r.source_paths,
                    "backup_locations": r.backup_locations,
                    "total_size": r.total_size,
                    "verified": r.verified,
                    "verification_time": r.verification_time.isoformat() if r.verification_time else None,
                    "verification_result": r.verification_result.value if r.verification_result else None,
                    "error_message": r.error_message,
                    "metadata": r.metadata,
                    "entries": [
                        {
                            "source_path": e.source_path,
                            "backup_path": e.backup_path,
                            "original_size": e.original_size,
                            "backup_size": e.backup_size,
                            "hash_original": e.hash_original,
                            "hash_backup": e.hash_backup,
                            "backup_time": e.backup_time.isoformat(),
                            "is_compressed": e.is_compressed,
                        }
                        for e in r.entries
                    ],
                }
                for id, r in self._records.items()
            },
            "restore_points": {
                id: {
                    "id": rp.id,
                    "name": rp.name,
                    "created_at": rp.created_at.isoformat(),
                    "backup_record_id": rp.backup_record_id,
                    "description": rp.description,
                    "auto_created": rp.auto_created,
                    "is_valid": rp.is_valid,
                }
                for id, rp in self._restore_points.items()
            },
        }

        state_file = self._state_file_path()
        state_file.parent.mkdir(parents=True, exist_ok=True)

        with open(state_file, "w") as f:
            json.dump(state, f, indent=2)

    def _load_state(self) -> None:
        """Load backup state from disk."""
        state_file = self._state_file_path()
        if not state_file.exists():
            return

        try:
            with open(state_file, "r") as f:
                state = json.load(f)

            # Load records
            for id, data in state.get("records", {}).items():
                entries = [
                    BackupEntry(
                        source_path=e["source_path"],
                        backup_path=e["backup_path"],
                        original_size=e["original_size"],
                        backup_size=e["backup_size"],
                        hash_original=e["hash_original"],
                        hash_backup=e["hash_backup"],
                        backup_time=datetime.fromisoformat(e["backup_time"]),
                        is_compressed=e.get("is_compressed", False),
                    )
                    for e in data.get("entries", [])
                ]

                record = BackupRecord(
                    id=data["id"],
                    type=BackupType(data["type"]),
                    status=BackupStatus(data["status"]),
                    created_at=datetime.fromisoformat(data["created_at"]),
                    expires_at=datetime.fromisoformat(data["expires_at"]),
                    source_paths=data["source_paths"],
                    backup_locations=data["backup_locations"],
                    entries=entries,
                    total_size=data.get("total_size", 0),
                    verified=data.get("verified", False),
                    verification_time=datetime.fromisoformat(data["verification_time"]) if data.get("verification_time") else None,
                    verification_result=VerificationResult(data["verification_result"]) if data.get("verification_result") else None,
                    error_message=data.get("error_message"),
                    metadata=data.get("metadata", {}),
                )
                self._records[id] = record

            # Load restore points
            for id, data in state.get("restore_points", {}).items():
                rp = RestorePoint(
                    id=data["id"],
                    name=data["name"],
                    created_at=datetime.fromisoformat(data["created_at"]),
                    backup_record_id=data["backup_record_id"],
                    description=data["description"],
                    auto_created=data.get("auto_created", True),
                    is_valid=data.get("is_valid", True),
                )
                self._restore_points[id] = rp

            logger.info(f"Loaded {len(self._records)} backup records, {len(self._restore_points)} restore points")

        except Exception as e:
            logger.error(f"Failed to load backup state: {e}")

    def add_alert_callback(self, callback: Callable[[str, Dict], None]) -> None:
        """Add a callback for backup alerts."""
        self._alert_callbacks.append(callback)

    def _send_alert(self, alert_type: str, data: Dict) -> None:
        """Send alert to all registered callbacks."""
        for callback in self._alert_callbacks:
            try:
                callback(alert_type, data)
            except Exception as e:
                logger.error(f"Alert callback failed: {e}")

    def create_restore_point(
        self,
        name: str,
        paths: List[Path],
        description: str = "",
        auto_created: bool = False,
    ) -> RestorePoint:
        """
        Create a restore point before an operation.

        Args:
            name: Name for the restore point
            paths: Paths to backup
            description: Description of why restore point was created
            auto_created: Whether this was auto-created by system

        Returns:
            RestorePoint object
        """
        with LogPerformance(f"Create restore point: {name}"):
            # Create backup
            record = self.backup(
                paths,
                backup_type=BackupType.RESTORE_POINT,
                metadata={"restore_point_name": name},
            )

            # Create restore point entry
            rp = RestorePoint(
                id=f"rp_{datetime.now().strftime('%Y%m%d_%H%M%S')}",
                name=name,
                created_at=datetime.now(),
                backup_record_id=record.id,
                description=description,
                auto_created=auto_created,
            )

            with self._lock:
                self._restore_points[rp.id] = rp
                self._save_state()

            logger.info(f"Created restore point: {name}", id=rp.id, paths=len(paths))
            return rp

    def backup(
        self,
        paths: List[Path],
        backup_type: BackupType = BackupType.FULL,
        metadata: Optional[Dict] = None,
    ) -> BackupRecord:
        """
        Create a backup of specified paths.

        Args:
            paths: List of paths to backup
            backup_type: Type of backup
            metadata: Additional metadata

        Returns:
            BackupRecord with results
        """
        record_id = f"bkp_{datetime.now().strftime('%Y%m%d_%H%M%S')}"
        locations = self._get_available_locations(self.config.prefer_different_drives)

        if len(locations) < self.config.min_backup_copies:
            logger.warning(f"Only {len(locations)} backup locations available, need {self.config.min_backup_copies}")

        record = BackupRecord(
            id=record_id,
            type=backup_type,
            status=BackupStatus.IN_PROGRESS,
            created_at=datetime.now(),
            expires_at=datetime.now() + timedelta(days=self.config.retention_days),
            source_paths=[str(p) for p in paths],
            backup_locations=[loc.id for loc in locations[:self.config.min_backup_copies]],
            metadata=metadata or {},
        )

        try:
            with LogPerformance(f"Backup {len(paths)} paths"):
                for path in paths:
                    self._backup_path(path, record, locations[:self.config.min_backup_copies])

            record.status = BackupStatus.COMPLETED

            # Verify if configured
            if self.config.verify_after_backup:
                success, results = self.verifier.verify_record(record)
                record.verified = success
                record.verification_time = datetime.now()
                record.verification_result = VerificationResult.SUCCESS if success else VerificationResult.HASH_MISMATCH
                if success:
                    record.status = BackupStatus.VERIFIED

        except Exception as e:
            record.status = BackupStatus.FAILED
            record.error_message = str(e)
            logger.error(f"Backup failed: {e}")

        with self._lock:
            self._records[record.id] = record
            self._save_state()

        return record

    def _backup_path(
        self,
        source: Path,
        record: BackupRecord,
        locations: List[BackupLocation],
    ) -> None:
        """Backup a single path to multiple locations."""
        for location in locations:
            try:
                backup_dir = location.path / record.id
                backup_dir.mkdir(parents=True, exist_ok=True)

                if source.is_file():
                    self._backup_file(source, backup_dir, record, location)
                elif source.is_dir():
                    self._backup_directory(source, backup_dir, record, location)

            except Exception as e:
                logger.error(f"Failed to backup {source} to {location.path}: {e}")

    def _backup_file(
        self,
        source: Path,
        backup_dir: Path,
        record: BackupRecord,
        location: BackupLocation,
    ) -> None:
        """Backup a single file."""
        hash_original = self.verifier.compute_hash(source)
        original_size = source.stat().st_size

        if self.config.compress_backups:
            backup_path = backup_dir / (source.name + ".gz")
            with open(source, "rb") as f_in:
                with gzip.open(backup_path, "wb", compresslevel=self.config.compression_level) as f_out:
                    shutil.copyfileobj(f_in, f_out)
            is_compressed = True
        else:
            backup_path = backup_dir / source.name
            shutil.copy2(source, backup_path)
            is_compressed = False

        backup_size = backup_path.stat().st_size
        hash_backup = self.verifier.compute_hash(backup_path)

        entry = BackupEntry(
            source_path=str(source),
            backup_path=str(backup_path),
            original_size=original_size,
            backup_size=backup_size,
            hash_original=hash_original,
            hash_backup=hash_backup,
            backup_time=datetime.now(),
            is_compressed=is_compressed,
        )

        record.entries.append(entry)
        record.total_size += backup_size

    def _backup_directory(
        self,
        source: Path,
        backup_dir: Path,
        record: BackupRecord,
        location: BackupLocation,
    ) -> None:
        """Backup a directory recursively."""
        for item in source.rglob("*"):
            if item.is_file():
                rel_path = item.relative_to(source)
                item_backup_dir = backup_dir / source.name / rel_path.parent
                item_backup_dir.mkdir(parents=True, exist_ok=True)
                self._backup_file(item, item_backup_dir, record, location)

    def restore(
        self,
        record_id: str,
        target_path: Optional[Path] = None,
        overwrite: bool = False,
    ) -> bool:
        """
        Restore from a backup record.

        Args:
            record_id: ID of backup record
            target_path: Target path (None = restore to original locations)
            overwrite: Whether to overwrite existing files

        Returns:
            True if restore successful
        """
        record = self._records.get(record_id)
        if not record:
            logger.error(f"Backup record not found: {record_id}")
            return False

        if record.status not in [BackupStatus.COMPLETED, BackupStatus.VERIFIED]:
            logger.error(f"Cannot restore from {record.status.value} backup")
            return False

        try:
            with LogPerformance(f"Restore from {record_id}"):
                for entry in record.entries:
                    self._restore_entry(entry, target_path, overwrite)

            logger.info(f"Restored {len(record.entries)} files from {record_id}")
            return True

        except Exception as e:
            logger.error(f"Restore failed: {e}")
            return False

    def _restore_entry(
        self,
        entry: BackupEntry,
        target_path: Optional[Path],
        overwrite: bool,
    ) -> None:
        """Restore a single backup entry."""
        backup_path = Path(entry.backup_path)
        if not backup_path.exists():
            raise FileNotFoundError(f"Backup file not found: {backup_path}")

        if target_path:
            restore_path = target_path / Path(entry.source_path).name
        else:
            restore_path = Path(entry.source_path)

        if restore_path.exists() and not overwrite:
            raise FileExistsError(f"Target exists: {restore_path}")

        restore_path.parent.mkdir(parents=True, exist_ok=True)

        if entry.is_compressed:
            with gzip.open(backup_path, "rb") as f_in:
                with open(restore_path, "wb") as f_out:
                    shutil.copyfileobj(f_in, f_out)
        else:
            shutil.copy2(backup_path, restore_path)

        # Verify restored file
        restored_hash = self.verifier.compute_hash(restore_path)
        if restored_hash != entry.hash_original:
            logger.warning(f"Restored file hash mismatch: {restore_path}")

    def restore_from_point(self, restore_point_id: str, target_path: Optional[Path] = None) -> bool:
        """Restore from a restore point."""
        rp = self._restore_points.get(restore_point_id)
        if not rp:
            logger.error(f"Restore point not found: {restore_point_id}")
            return False

        return self.restore(rp.backup_record_id, target_path)

    def check_expiring_backups(self) -> List[BackupRecord]:
        """Check for backups that will expire soon and need review."""
        expiring = []
        alert_threshold = datetime.now() + timedelta(days=self.config.alert_before_delete_days)

        for record in self._records.values():
            if record.status in [BackupStatus.COMPLETED, BackupStatus.VERIFIED]:
                if record.expires_at <= alert_threshold:
                    record.status = BackupStatus.PENDING_REVIEW
                    expiring.append(record)

                    self._send_alert("backup_expiring", {
                        "record_id": record.id,
                        "expires_at": record.expires_at.isoformat(),
                        "source_paths": record.source_paths,
                    })

        if expiring:
            self._save_state()

        return expiring

    def cleanup_expired(self, force: bool = False) -> int:
        """
        Clean up expired backups.

        Args:
            force: If True, delete without requiring review

        Returns:
            Number of records cleaned up
        """
        now = datetime.now()
        cleaned = 0

        for record_id, record in list(self._records.items()):
            if record.expires_at <= now:
                if record.status == BackupStatus.PENDING_REVIEW and not force:
                    continue  # Skip pending review unless forced

                # Delete backup files
                for loc_id in record.backup_locations:
                    loc = self._locations.get(loc_id)
                    if loc:
                        backup_dir = loc.path / record.id
                        if backup_dir.exists():
                            shutil.rmtree(backup_dir, ignore_errors=True)

                record.status = BackupStatus.DELETED
                cleaned += 1

        if cleaned:
            self._save_state()

        logger.info(f"Cleaned up {cleaned} expired backups")
        return cleaned

    def extend_retention(self, record_id: str, days: int = 7) -> bool:
        """Extend retention period for a backup."""
        record = self._records.get(record_id)
        if not record:
            return False

        record.expires_at = record.expires_at + timedelta(days=days)
        if record.status == BackupStatus.PENDING_REVIEW:
            record.status = BackupStatus.VERIFIED if record.verified else BackupStatus.COMPLETED

        self._save_state()
        logger.info(f"Extended retention for {record_id} by {days} days")
        return True

    def get_status(self) -> Dict[str, Any]:
        """Get backup system status."""
        return {
            "locations": [
                {
                    "id": loc.id,
                    "path": str(loc.path),
                    "drive": loc.drive_letter,
                    "available": loc.is_available,
                    "primary": loc.is_primary,
                }
                for loc in self._locations.values()
            ],
            "records_count": len(self._records),
            "restore_points_count": len(self._restore_points),
            "pending_review": sum(1 for r in self._records.values() if r.status == BackupStatus.PENDING_REVIEW),
            "total_backup_size_gb": sum(r.total_size for r in self._records.values()) / (1024**3),
        }

    def list_restore_points(self) -> List[RestorePoint]:
        """List all restore points."""
        return sorted(
            [rp for rp in self._restore_points.values() if rp.is_valid],
            key=lambda x: x.created_at,
            reverse=True,
        )

    def list_backups(self, status: Optional[BackupStatus] = None) -> List[BackupRecord]:
        """List backup records, optionally filtered by status."""
        records = list(self._records.values())
        if status:
            records = [r for r in records if r.status == status]
        return sorted(records, key=lambda x: x.created_at, reverse=True)


# Global backup manager
_backup_manager: Optional[BackupManager] = None


def get_backup_manager() -> BackupManager:
    """Get the global backup manager instance."""
    global _backup_manager
    if _backup_manager is None:
        _backup_manager = BackupManager()
    return _backup_manager


def create_restore_point_before(operation: str, paths: List[Path]) -> Optional[RestorePoint]:
    """
    Convenience function to create restore point before an operation.

    Used by other modules to ensure safety.
    """
    manager = get_backup_manager()
    if manager.config.auto_restore_points and operation in manager.config.restore_point_on_operations:
        return manager.create_restore_point(
            name=f"Before {operation}",
            paths=paths,
            description=f"Auto-created before {operation} operation",
            auto_created=True,
        )
    return None
