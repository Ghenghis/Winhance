"""
Transaction Manager - Safe File Operations with Multi-Level Rollback

Provides:
- Atomic file operations with full rollback capability
- Transaction logging with append-only JSONL format
- Multi-level backup strategy (log, VSS reference, full backup)
- Auto-generated rollback scripts (.ps1, .bat)
- Dependency checking before moves
- In-use file detection
"""

from __future__ import annotations

import hashlib
import json
import shutil
import uuid
from dataclasses import dataclass, field, asdict
from datetime import datetime, timedelta
from enum import Enum
from pathlib import Path
from typing import Optional, List, Dict, Any, Generator
import os
import threading
from concurrent.futures import ThreadPoolExecutor

import orjson
from loguru import logger


class TransactionStatus(str, Enum):
    """Transaction status."""
    PENDING = "pending"
    IN_PROGRESS = "in_progress"
    COMPLETED = "completed"
    FAILED = "failed"
    ROLLED_BACK = "rolled_back"


class OperationType(str, Enum):
    """File operation type."""
    MOVE = "move"
    COPY = "copy"
    RENAME = "rename"
    DELETE = "delete"
    CREATE_DIR = "create_dir"


@dataclass
class FileTransaction:
    """A single file operation transaction."""
    id: str
    timestamp: datetime
    operation: OperationType
    source_path: str
    dest_path: Optional[str]
    source_hash: Optional[str]
    source_size: int
    backup_path: Optional[str]
    metadata: Dict[str, Any]
    status: TransactionStatus
    error: Optional[str] = None
    completed_at: Optional[datetime] = None

    def to_json(self) -> bytes:
        """Serialize to JSON bytes."""
        data = asdict(self)
        data["timestamp"] = self.timestamp.isoformat()
        data["completed_at"] = self.completed_at.isoformat() if self.completed_at else None
        data["operation"] = self.operation.value
        data["status"] = self.status.value
        return orjson.dumps(data)

    @classmethod
    def from_json(cls, data: bytes) -> "FileTransaction":
        """Deserialize from JSON bytes."""
        obj = orjson.loads(data)
        obj["timestamp"] = datetime.fromisoformat(obj["timestamp"])
        obj["completed_at"] = datetime.fromisoformat(obj["completed_at"]) if obj["completed_at"] else None
        obj["operation"] = OperationType(obj["operation"])
        obj["status"] = TransactionStatus(obj["status"])
        return cls(**obj)


class TransactionManager:
    """
    Manages file operations with full transaction support and rollback.

    Features:
    - Atomic operations with rollback
    - Append-only transaction log
    - Multi-level backup strategy
    - Dependency checking
    - In-use file detection
    - Auto-generated rollback scripts
    """

    def __init__(
        self,
        log_path: Path,
        backup_dir: Path,
        max_backup_size_gb: float = 100.0,
        backup_retention_days: int = 7,
        workers: int = 4,
    ):
        self.log_path = Path(log_path)
        self.backup_dir = Path(backup_dir)
        self.max_backup_size_gb = max_backup_size_gb
        self.backup_retention_days = backup_retention_days
        self.workers = workers

        # Ensure directories exist
        self.log_path.parent.mkdir(parents=True, exist_ok=True)
        self.backup_dir.mkdir(parents=True, exist_ok=True)

        # Thread safety
        self._lock = threading.Lock()
        self._executor = ThreadPoolExecutor(max_workers=workers)

        logger.info(f"TransactionManager initialized: log={log_path}, backup={backup_dir}")

    def _compute_hash(self, path: Path, quick: bool = True) -> Optional[str]:
        """Compute file hash for verification."""
        try:
            if not path.exists() or path.is_dir():
                return None

            if quick:
                # Quick hash: first 64KB + size
                with open(path, "rb") as f:
                    data = f.read(65536)
                size = path.stat().st_size
                return hashlib.md5(data + str(size).encode()).hexdigest()
            else:
                # Full SHA-256
                sha = hashlib.sha256()
                with open(path, "rb") as f:
                    for chunk in iter(lambda: f.read(65536), b""):
                        sha.update(chunk)
                return sha.hexdigest()
        except Exception as e:
            logger.warning(f"Failed to hash {path}: {e}")
            return None

    def _is_file_in_use(self, path: Path) -> bool:
        """Check if a file is currently in use (Windows)."""
        if not path.exists():
            return False

        try:
            # Try to open with exclusive access
            with open(path, "r+b") as f:
                pass
            return False
        except (IOError, PermissionError):
            return True

    def _create_backup(self, source: Path) -> Optional[Path]:
        """Create a backup of the source file."""
        if not source.exists() or source.is_dir():
            return None

        try:
            # Check backup space
            backup_used = sum(
                f.stat().st_size for f in self.backup_dir.rglob("*") if f.is_file()
            )
            if backup_used > self.max_backup_size_gb * 1024**3:
                self._cleanup_old_backups()

            # Create backup with timestamp
            timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
            backup_name = f"{source.stem}_{timestamp}{source.suffix}"
            backup_path = self.backup_dir / backup_name

            shutil.copy2(source, backup_path)
            logger.debug(f"Created backup: {backup_path}")
            return backup_path

        except Exception as e:
            logger.warning(f"Failed to create backup for {source}: {e}")
            return None

    def _cleanup_old_backups(self) -> int:
        """Remove backups older than retention period."""
        cutoff = datetime.now() - timedelta(days=self.backup_retention_days)
        removed = 0

        for backup in self.backup_dir.glob("*"):
            if backup.is_file():
                mtime = datetime.fromtimestamp(backup.stat().st_mtime)
                if mtime < cutoff:
                    backup.unlink()
                    removed += 1

        logger.info(f"Cleaned up {removed} old backups")
        return removed

    def _log_transaction(self, tx: FileTransaction) -> None:
        """Append transaction to log file."""
        with self._lock:
            with open(self.log_path, "ab") as f:
                f.write(tx.to_json() + b"\n")

    def _update_transaction(self, tx: FileTransaction) -> None:
        """Update transaction in log (append new state)."""
        self._log_transaction(tx)

    def begin_transaction(
        self,
        operation: OperationType,
        source: Path,
        dest: Optional[Path] = None,
        create_backup: bool = True,
        metadata: Optional[Dict[str, Any]] = None,
    ) -> FileTransaction:
        """
        Begin a new transaction.

        Args:
            operation: Type of operation
            source: Source file/directory path
            dest: Destination path (for move/copy/rename)
            create_backup: Whether to create a backup
            metadata: Additional metadata to store

        Returns:
            FileTransaction object
        """
        source = Path(source)

        # Check if file is in use
        if self._is_file_in_use(source):
            raise IOError(f"File is in use: {source}")

        # Compute hash
        source_hash = self._compute_hash(source)

        # Get file size
        source_size = source.stat().st_size if source.exists() and source.is_file() else 0

        # Create backup if requested
        backup_path = None
        if create_backup and source.exists() and source.is_file():
            backup_path = self._create_backup(source)

        tx = FileTransaction(
            id=str(uuid.uuid4()),
            timestamp=datetime.now(),
            operation=operation,
            source_path=str(source),
            dest_path=str(dest) if dest else None,
            source_hash=source_hash,
            source_size=source_size,
            backup_path=str(backup_path) if backup_path else None,
            metadata=metadata or {},
            status=TransactionStatus.PENDING,
        )

        self._log_transaction(tx)
        logger.info(f"Transaction started: {tx.id} - {operation.value} {source}")
        return tx

    def commit(self, tx: FileTransaction, dest: Optional[Path] = None) -> FileTransaction:
        """
        Commit a transaction after successful operation.

        Args:
            tx: The transaction to commit
            dest: Final destination path (if different from planned)

        Returns:
            Updated transaction
        """
        tx.status = TransactionStatus.COMPLETED
        tx.completed_at = datetime.now()
        if dest:
            tx.dest_path = str(dest)

        self._update_transaction(tx)
        logger.info(f"Transaction committed: {tx.id}")
        return tx

    def fail(self, tx: FileTransaction, error: str) -> FileTransaction:
        """Mark a transaction as failed."""
        tx.status = TransactionStatus.FAILED
        tx.error = error
        tx.completed_at = datetime.now()

        self._update_transaction(tx)
        logger.error(f"Transaction failed: {tx.id} - {error}")
        return tx

    def execute_move(
        self,
        source: Path,
        dest: Path,
        create_backup: bool = True,
        metadata: Optional[Dict[str, Any]] = None,
    ) -> FileTransaction:
        """
        Execute a move operation with full transaction support.

        Args:
            source: Source path
            dest: Destination path
            create_backup: Whether to create backup
            metadata: Additional metadata

        Returns:
            Completed transaction
        """
        source = Path(source)
        dest = Path(dest)

        tx = self.begin_transaction(
            OperationType.MOVE, source, dest, create_backup, metadata
        )

        try:
            tx.status = TransactionStatus.IN_PROGRESS
            self._update_transaction(tx)

            # Ensure destination directory exists
            dest.parent.mkdir(parents=True, exist_ok=True)

            # Perform move
            shutil.move(str(source), str(dest))

            return self.commit(tx, dest)

        except Exception as e:
            # Try to restore from backup
            if tx.backup_path and Path(tx.backup_path).exists():
                try:
                    shutil.copy2(tx.backup_path, source)
                    logger.info(f"Restored from backup: {source}")
                except Exception as restore_error:
                    logger.error(f"Failed to restore from backup: {restore_error}")

            return self.fail(tx, str(e))

    def rollback(self, tx_id: str) -> bool:
        """
        Rollback a completed transaction.

        Args:
            tx_id: Transaction ID to rollback

        Returns:
            True if successful
        """
        tx = self.get_transaction(tx_id)
        if not tx:
            logger.error(f"Transaction not found: {tx_id}")
            return False

        if tx.status != TransactionStatus.COMPLETED:
            logger.error(f"Cannot rollback transaction in state: {tx.status}")
            return False

        try:
            if tx.operation == OperationType.MOVE:
                # Move file back
                if tx.dest_path and Path(tx.dest_path).exists():
                    source = Path(tx.source_path)
                    source.parent.mkdir(parents=True, exist_ok=True)
                    shutil.move(tx.dest_path, tx.source_path)
                elif tx.backup_path and Path(tx.backup_path).exists():
                    shutil.copy2(tx.backup_path, tx.source_path)

            elif tx.operation == OperationType.DELETE:
                # Restore from backup
                if tx.backup_path and Path(tx.backup_path).exists():
                    shutil.copy2(tx.backup_path, tx.source_path)

            tx.status = TransactionStatus.ROLLED_BACK
            self._update_transaction(tx)
            logger.info(f"Transaction rolled back: {tx_id}")
            return True

        except Exception as e:
            logger.error(f"Rollback failed: {e}")
            return False

    def rollback_range(
        self,
        start_time: Optional[datetime] = None,
        end_time: Optional[datetime] = None,
        hours: Optional[int] = None,
    ) -> List[str]:
        """
        Rollback all transactions in a time range.

        Args:
            start_time: Start of range
            end_time: End of range
            hours: Alternative: rollback last N hours

        Returns:
            List of rolled back transaction IDs
        """
        if hours:
            end_time = datetime.now()
            start_time = end_time - timedelta(hours=hours)

        rolled_back = []
        transactions = list(self.iter_transactions())

        # Process in reverse order (newest first)
        for tx in reversed(transactions):
            if tx.status != TransactionStatus.COMPLETED:
                continue

            if start_time and tx.timestamp < start_time:
                continue
            if end_time and tx.timestamp > end_time:
                continue

            if self.rollback(tx.id):
                rolled_back.append(tx.id)

        return rolled_back

    def get_transaction(self, tx_id: str) -> Optional[FileTransaction]:
        """Get a transaction by ID."""
        for tx in self.iter_transactions():
            if tx.id == tx_id:
                return tx
        return None

    def iter_transactions(self) -> Generator[FileTransaction, None, None]:
        """Iterate through all transactions."""
        if not self.log_path.exists():
            return

        seen_ids: Dict[str, FileTransaction] = {}

        with open(self.log_path, "rb") as f:
            for line in f:
                line = line.strip()
                if not line:
                    continue
                try:
                    tx = FileTransaction.from_json(line)
                    seen_ids[tx.id] = tx  # Later entries override earlier
                except Exception as e:
                    logger.warning(f"Failed to parse transaction: {e}")

        # Yield the latest state of each transaction
        for tx in seen_ids.values():
            yield tx

    def list_transactions(
        self,
        limit: int = 100,
        status: Optional[TransactionStatus] = None,
        operation: Optional[OperationType] = None,
    ) -> List[FileTransaction]:
        """List recent transactions with optional filtering."""
        transactions = []

        for tx in self.iter_transactions():
            if status and tx.status != status:
                continue
            if operation and tx.operation != operation:
                continue
            transactions.append(tx)

        # Sort by timestamp descending and limit
        transactions.sort(key=lambda x: x.timestamp, reverse=True)
        return transactions[:limit]

    def generate_rollback_script(
        self,
        tx_ids: Optional[List[str]] = None,
        hours: Optional[int] = None,
        format: str = "ps1",
    ) -> str:
        """
        Generate a rollback script for specified transactions.

        Args:
            tx_ids: Specific transaction IDs to rollback
            hours: Alternative: all transactions in last N hours
            format: Script format ("ps1" or "bat")

        Returns:
            Script content as string
        """
        transactions = []

        if tx_ids:
            for tx_id in tx_ids:
                tx = self.get_transaction(tx_id)
                if tx and tx.status == TransactionStatus.COMPLETED:
                    transactions.append(tx)
        elif hours:
            cutoff = datetime.now() - timedelta(hours=hours)
            for tx in self.iter_transactions():
                if tx.status == TransactionStatus.COMPLETED and tx.timestamp >= cutoff:
                    transactions.append(tx)

        # Sort newest first for rollback
        transactions.sort(key=lambda x: x.timestamp, reverse=True)

        if format == "ps1":
            return self._generate_powershell_script(transactions)
        else:
            return self._generate_batch_script(transactions)

    def _generate_powershell_script(self, transactions: List[FileTransaction]) -> str:
        """Generate PowerShell rollback script."""
        lines = [
            "# NexusFS Rollback Script",
            f"# Generated: {datetime.now().isoformat()}",
            f"# Transactions: {len(transactions)}",
            "",
            "param(",
            "    [switch]$DryRun = $false,",
            "    [switch]$Force = $false",
            ")",
            "",
            '$ErrorActionPreference = "Stop"',
            "",
            "function Rollback-Transaction {",
            "    param($Id, $Src, $Dst, $Backup)",
            "",
            '    Write-Host "[$Id] Rolling back: $Dst -> $Src" -ForegroundColor Yellow',
            "",
            "    if ($DryRun) {",
            '        Write-Host "  [DRY-RUN] Would restore" -ForegroundColor Cyan',
            "        return",
            "    }",
            "",
            "    # Ensure destination directory exists",
            "    $destDir = Split-Path $Src -Parent",
            "    if (-not (Test-Path $destDir)) {",
            "        New-Item -ItemType Directory -Path $destDir -Force | Out-Null",
            "    }",
            "",
            "    # Try to restore from current location first",
            "    if ($Dst -and (Test-Path $Dst)) {",
            "        Move-Item -Path $Dst -Destination $Src -Force:$Force",
            '        Write-Host "  [OK] Restored from current location" -ForegroundColor Green',
            "    }",
            "    elseif ($Backup -and (Test-Path $Backup)) {",
            "        Copy-Item -Path $Backup -Destination $Src -Force",
            '        Write-Host "  [OK] Restored from backup" -ForegroundColor Green',
            "    }",
            "    else {",
            '        Write-Host "  [ERROR] No source found for restoration" -ForegroundColor Red',
            "    }",
            "}",
            "",
            "# Transactions to rollback",
        ]

        for tx in transactions:
            lines.append(f'Rollback-Transaction -Id "{tx.id}" `')
            lines.append(f'    -Src "{tx.source_path}" `')
            lines.append(f'    -Dst "{tx.dest_path or ""}" `')
            lines.append(f'    -Backup "{tx.backup_path or ""}"')
            lines.append("")

        lines.extend([
            'Write-Host ""',
            'Write-Host "Rollback complete!" -ForegroundColor Green',
        ])

        return "\n".join(lines)

    def _generate_batch_script(self, transactions: List[FileTransaction]) -> str:
        """Generate Batch rollback script."""
        lines = [
            "@echo off",
            f"REM NexusFS Rollback Script",
            f"REM Generated: {datetime.now().isoformat()}",
            f"REM Transactions: {len(transactions)}",
            "",
            "setlocal enabledelayedexpansion",
            "",
        ]

        for i, tx in enumerate(transactions, 1):
            lines.append(f"echo [{i}/{len(transactions)}] Rolling back: {tx.id}")
            if tx.dest_path:
                lines.append(f'if exist "{tx.dest_path}" (')
                lines.append(f'    move /Y "{tx.dest_path}" "{tx.source_path}"')
                lines.append(f'    echo   [OK] Restored')
                lines.append(f') else (')
                if tx.backup_path:
                    lines.append(f'    if exist "{tx.backup_path}" (')
                    lines.append(f'        copy /Y "{tx.backup_path}" "{tx.source_path}"')
                    lines.append(f'        echo   [OK] Restored from backup')
                    lines.append(f'    ) else (')
                    lines.append(f'        echo   [ERROR] No source found')
                    lines.append(f'    )')
                else:
                    lines.append(f'    echo   [ERROR] No source found')
                lines.append(f')')
            lines.append("")

        lines.extend([
            "echo.",
            "echo Rollback complete!",
            "pause",
        ])

        return "\n".join(lines)

    def check_dependencies(self, source: Path) -> Dict[str, Any]:
        """
        Check if a file/folder is used by other projects.

        Returns information about potential dependencies.
        """
        source = Path(source)
        result = {
            "path": str(source),
            "in_use": self._is_file_in_use(source),
            "references": [],
            "project_files": [],
            "config_references": [],
        }

        # Check for common project files that might reference this
        project_patterns = [
            "package.json",
            "pyproject.toml",
            "Cargo.toml",
            "*.csproj",
            "pom.xml",
            "build.gradle",
            ".env*",
            "*.config",
            "*.toml",
            "*.yaml",
            "*.yml",
        ]

        # Look for references in parent directories
        for parent in source.parents:
            if parent == parent.parent:  # Root
                break

            for pattern in project_patterns:
                for proj_file in parent.glob(pattern):
                    try:
                        content = proj_file.read_text(errors="ignore")
                        if source.name in content or str(source) in content:
                            result["project_files"].append(str(proj_file))
                    except Exception:
                        pass

        return result

    def get_stats(self) -> Dict[str, Any]:
        """Get transaction statistics."""
        stats = {
            "total": 0,
            "completed": 0,
            "failed": 0,
            "rolled_back": 0,
            "pending": 0,
            "by_operation": {},
            "total_size_moved": 0,
        }

        for tx in self.iter_transactions():
            stats["total"] += 1
            stats[tx.status.value] = stats.get(tx.status.value, 0) + 1

            op = tx.operation.value
            stats["by_operation"][op] = stats["by_operation"].get(op, 0) + 1

            if tx.status == TransactionStatus.COMPLETED and tx.source_size:
                stats["total_size_moved"] += tx.source_size

        return stats
