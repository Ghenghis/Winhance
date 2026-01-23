"""
Real-Time Agent-Based File Organizer

Game-changing features:
- Watches folders in real-time
- AI-powered organization decisions
- User preference learning
- Smart categorization
- Touch-friendly confirmations
- Undo support
"""

from __future__ import annotations

import logging
import shutil
import json
import time
from dataclasses import dataclass, field
from datetime import datetime, timedelta
from enum import Enum
from pathlib import Path
from typing import Dict, List, Optional, Callable, Any, Set, TYPE_CHECKING
import threading
import queue

from watchdog.observers import Observer
from watchdog.events import FileSystemEventHandler, FileSystemEvent, FileMovedEvent

if TYPE_CHECKING:
    from nexus_ai.tools.file_classifier import FileClassification

# Setup logger with fallback
_logger: Optional[logging.Logger] = None


def _get_module_logger() -> logging.Logger:
    """Get logger with fallback."""
    global _logger
    if _logger is None:
        try:
            from nexus_ai.core.logging_config import get_logger
            _logger = get_logger("realtime_organizer")
        except ImportError:
            _logger = logging.getLogger("realtime_organizer")
    return _logger


logger = _get_module_logger()


class SafetyLevel(Enum):
    """File safety levels."""
    CRITICAL = "critical"
    PROTECTED = "protected"
    INSTALLED = "installed"
    CAUTIOUS = "cautious"
    SAFE = "safe"
    TEMPORARY = "temporary"


class FileOrigin(Enum):
    """Where a file came from."""
    WINDOWS_SYSTEM = "windows_system"
    WINDOWS_UPDATE = "windows_update"
    DRIVER = "driver"
    INSTALLED_APP = "installed_app"
    USER_CREATED = "user_created"
    USER_DOWNLOADED = "user_downloaded"
    APP_GENERATED = "app_generated"
    TEMP_FILE = "temp_file"
    CACHE = "cache"
    UNKNOWN = "unknown"


def get_classifier() -> Any:
    """Get file classifier with fallback."""
    try:
        from nexus_ai.tools.file_classifier import get_classifier as _get_classifier
        return _get_classifier()
    except ImportError:
        return None


class OrganizationAction(Enum):
    """Actions the organizer can take."""
    MOVE = "move"
    COPY = "copy"
    RENAME = "rename"
    DELETE = "delete"
    ARCHIVE = "archive"
    TAG = "tag"
    IGNORE = "ignore"
    ASK_USER = "ask_user"


class ConfirmationLevel(Enum):
    """When to ask for confirmation."""
    NEVER = "never"           # Fully automatic
    CAUTIOUS = "cautious"     # Ask for uncertain files
    ALWAYS = "always"         # Always ask
    LEARNING = "learning"     # Ask initially, learn preferences


@dataclass
class OrganizationRule:
    """Rule for organizing files."""
    id: str
    name: str
    enabled: bool = True

    # Matching conditions
    extensions: List[str] = field(default_factory=list)
    name_patterns: List[str] = field(default_factory=list)
    source_folders: List[str] = field(default_factory=list)
    min_size_bytes: Optional[int] = None
    max_size_bytes: Optional[int] = None
    file_origin: Optional[FileOrigin] = None

    # Action
    action: OrganizationAction = OrganizationAction.MOVE
    destination: Optional[str] = None
    subfolder_pattern: Optional[str] = None  # e.g., "{year}/{month}"

    # Confirmation
    confirmation_level: ConfirmationLevel = ConfirmationLevel.CAUTIOUS

    # Priority (higher = checked first)
    priority: int = 0


@dataclass
class OrganizationDecision:
    """A decision about what to do with a file."""
    file_path: Path
    rule_id: Optional[str]
    action: OrganizationAction
    destination: Optional[Path]
    reason: str
    confidence: float
    classification: Optional[FileClassification] = None

    # User interaction
    requires_confirmation: bool = False
    user_confirmed: Optional[bool] = None
    user_feedback: Optional[str] = None

    # Timestamps
    created_at: datetime = field(default_factory=datetime.now)
    executed_at: Optional[datetime] = None

    # Undo support
    original_path: Optional[Path] = None
    can_undo: bool = True
    undo_deadline: Optional[datetime] = None


@dataclass
class UserPreference:
    """Learned user preference for organization."""
    pattern: str  # What triggered this preference
    action: OrganizationAction
    destination: Optional[str]
    times_confirmed: int = 1
    times_rejected: int = 0
    last_used: datetime = field(default_factory=datetime.now)

    @property
    def confidence(self) -> float:
        """Confidence in this preference based on history."""
        total = self.times_confirmed + self.times_rejected
        if total == 0:
            return 0.5
        return self.times_confirmed / total


class FileEventHandler(FileSystemEventHandler):
    """Handles file system events for real-time organization."""

    def __init__(self, organizer: "RealtimeOrganizer"):
        super().__init__()
        self.organizer = organizer
        self._debounce: Dict[str, float] = {}
        self._debounce_seconds = 2.0  # Wait for file to stabilize

    def on_created(self, event: FileSystemEvent):
        if event.is_directory:
            return

        src_path = event.src_path
        path = Path(src_path.decode() if isinstance(src_path, bytes) else src_path)

        # Debounce - wait for file to stabilize
        key = str(path)
        now = time.time()
        if key in self._debounce:
            if now - self._debounce[key] < self._debounce_seconds:
                return
        self._debounce[key] = now

        # Queue for processing
        self.organizer.queue_file(path, "created")

    def on_moved(self, event: FileSystemEvent):
        if event.is_directory:
            return

        if isinstance(event, FileMovedEvent):
            dst_path = event.dest_path
            dest_path_str = dst_path.decode() if isinstance(dst_path, bytes) else str(dst_path)
            dest_path = Path(dest_path_str)
            self.organizer.queue_file(dest_path, "moved")


class RealtimeOrganizer:
    """
    Real-time AI-powered file organizer.

    Features:
    - Watches multiple folders
    - AI-powered decision making
    - User preference learning
    - Touch-friendly confirmations
    - Full undo support
    - Batch processing
    """

    # Default organization rules
    DEFAULT_RULES = [
        OrganizationRule(
            id="images_to_pictures",
            name="Images to Pictures",
            extensions=[".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".heic"],
            action=OrganizationAction.MOVE,
            destination=str(Path.home() / "Pictures" / "Organized"),
            subfolder_pattern="{year}/{month}",
            priority=10,
        ),
        OrganizationRule(
            id="documents_organize",
            name="Documents Organization",
            extensions=[".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx"],
            action=OrganizationAction.MOVE,
            destination=str(Path.home() / "Documents" / "Organized"),
            subfolder_pattern="{year}",
            priority=10,
        ),
        OrganizationRule(
            id="videos_to_videos",
            name="Videos to Videos",
            extensions=[".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm"],
            action=OrganizationAction.MOVE,
            destination=str(Path.home() / "Videos" / "Organized"),
            priority=10,
        ),
        OrganizationRule(
            id="music_to_music",
            name="Music to Music",
            extensions=[".mp3", ".flac", ".wav", ".m4a", ".aac", ".ogg"],
            action=OrganizationAction.MOVE,
            destination=str(Path.home() / "Music" / "Organized"),
            priority=10,
        ),
        OrganizationRule(
            id="archives_extract",
            name="Extract Archives",
            extensions=[".zip", ".rar", ".7z", ".tar", ".gz"],
            action=OrganizationAction.ARCHIVE,
            confirmation_level=ConfirmationLevel.CAUTIOUS,
            priority=5,
        ),
        OrganizationRule(
            id="installers_software",
            name="Installers to Software",
            extensions=[".exe", ".msi"],
            name_patterns=["*setup*", "*install*", "*installer*"],
            action=OrganizationAction.MOVE,
            destination=str(Path.home() / "Downloads" / "Software"),
            confirmation_level=ConfirmationLevel.ALWAYS,
            priority=5,
        ),
    ]

    def __init__(
        self,
        config_path: Optional[Path] = None,
        confirmation_callback: Optional[Callable[[OrganizationDecision], bool]] = None,
    ):
        self.config_path = config_path or Path.home() / ".nexusfs" / "organizer_config.json"
        self.confirmation_callback = confirmation_callback

        # State
        self.rules: List[OrganizationRule] = []
        self.preferences: Dict[str, UserPreference] = {}
        self.pending_decisions: Dict[str, OrganizationDecision] = {}
        self.decision_history: List[OrganizationDecision] = []
        self.undo_stack: List[OrganizationDecision] = []

        # Processing
        self._file_queue: "queue.Queue[Any]" = queue.Queue()
        self._running = False
        self._observer: Optional["Observer"] = None
        self._processor_thread: Optional[threading.Thread] = None
        self._watched_folders: Set[Path] = set()

        # Components
        self.classifier = get_classifier()

        # Load configuration
        self._load_config()

    def _load_config(self) -> None:
        """Load configuration and rules."""
        try:
            if self.config_path.exists():
                with open(self.config_path) as f:
                    config = json.load(f)
                    # Load rules
                    self.rules = [
                        OrganizationRule(**r) for r in config.get("rules", [])
                    ]
                    # Load preferences
                    for key, pref in config.get("preferences", {}).items():
                        self.preferences[key] = UserPreference(**pref)
            else:
                # Use default rules
                self.rules = self.DEFAULT_RULES.copy()
        except Exception as e:
            logger.error(f"Error loading config: {e}")
            self.rules = self.DEFAULT_RULES.copy()

    def _save_config(self) -> None:
        """Save configuration and learned preferences."""
        try:
            self.config_path.parent.mkdir(parents=True, exist_ok=True)

            config = {
                "rules": [
                    {
                        "id": r.id,
                        "name": r.name,
                        "enabled": r.enabled,
                        "extensions": r.extensions,
                        "name_patterns": r.name_patterns,
                        "source_folders": r.source_folders,
                        "min_size_bytes": r.min_size_bytes,
                        "max_size_bytes": r.max_size_bytes,
                        "action": r.action.value,
                        "destination": r.destination,
                        "subfolder_pattern": r.subfolder_pattern,
                        "confirmation_level": r.confirmation_level.value,
                        "priority": r.priority,
                    }
                    for r in self.rules
                ],
                "preferences": {
                    key: {
                        "pattern": p.pattern,
                        "action": p.action.value,
                        "destination": p.destination,
                        "times_confirmed": p.times_confirmed,
                        "times_rejected": p.times_rejected,
                        "last_used": p.last_used.isoformat(),
                    }
                    for key, p in self.preferences.items()
                },
            }

            with open(self.config_path, "w") as f:
                json.dump(config, f, indent=2)

        except Exception as e:
            logger.error(f"Error saving config: {e}")

    def add_watch_folder(self, folder: Path) -> None:
        """Add a folder to watch for new files."""
        folder = folder.resolve()
        if folder.exists() and folder.is_dir():
            self._watched_folders.add(folder)
            logger.info(f"Added watch folder: {folder}")

            # If already running, add to observer
            if self._observer and self._running:
                handler = FileEventHandler(self)
                self._observer.schedule(handler, str(folder), recursive=False)

    def remove_watch_folder(self, folder: Path) -> None:
        """Remove a folder from watching."""
        folder = folder.resolve()
        self._watched_folders.discard(folder)
        logger.info(f"Removed watch folder: {folder}")

    def start(self) -> None:
        """Start the real-time organizer."""
        if self._running:
            return

        self._running = True

        # Start file system observer
        self._observer = Observer()
        handler = FileEventHandler(self)
        for folder in self._watched_folders:
            self._observer.schedule(handler, str(folder), recursive=False)
        self._observer.start()

        # Start processor thread
        self._processor_thread = threading.Thread(target=self._process_queue, daemon=True)
        self._processor_thread.start()

        logger.info(f"Realtime organizer started, watching {len(self._watched_folders)} folders")

    def stop(self) -> None:
        """Stop the real-time organizer."""
        self._running = False

        if self._observer:
            self._observer.stop()
            self._observer.join(timeout=5.0)
            self._observer = None

        if self._processor_thread:
            self._processor_thread.join(timeout=5.0)
            self._processor_thread = None

        self._save_config()
        logger.info("Realtime organizer stopped")

    def queue_file(self, path: Path, event_type: str) -> None:
        """Queue a file for processing."""
        self._file_queue.put((path, event_type, datetime.now()))

    def _process_queue(self) -> None:
        """Process queued files."""
        while self._running:
            try:
                item = self._file_queue.get(timeout=1.0)
                path, event_type, timestamp = item

                # Skip if file no longer exists
                if not path.exists():
                    continue

                # Make decision
                decision = self._make_decision(path)

                # Handle based on confirmation needs
                if decision.requires_confirmation:
                    self._handle_confirmation_needed(decision)
                else:
                    self._execute_decision(decision)

            except queue.Empty:
                continue
            except Exception as e:
                logger.error(f"Error processing file: {e}")

    def _make_decision(self, path: Path) -> OrganizationDecision:
        """Make an organization decision for a file."""
        # Classify the file first
        classification = self.classifier.classify(path)

        # Check safety
        if classification.safety_level in [SafetyLevel.CRITICAL, SafetyLevel.PROTECTED]:
            return OrganizationDecision(
                file_path=path,
                rule_id=None,
                action=OrganizationAction.IGNORE,
                destination=None,
                reason=f"Protected file: {classification.warning_message}",
                confidence=1.0,
                classification=classification,
                requires_confirmation=False,
            )

        # Check installed app files
        if classification.origin == FileOrigin.INSTALLED_APP:
            return OrganizationDecision(
                file_path=path,
                rule_id=None,
                action=OrganizationAction.IGNORE,
                destination=None,
                reason=f"Part of installed app: {classification.associated_app}",
                confidence=0.9,
                classification=classification,
                requires_confirmation=False,
            )

        # Check user preferences first
        pref_key = self._get_preference_key(path)
        if pref_key in self.preferences:
            pref = self.preferences[pref_key]
            if pref.confidence > 0.8:
                return OrganizationDecision(
                    file_path=path,
                    rule_id=f"preference:{pref_key}",
                    action=pref.action,
                    destination=Path(pref.destination) if pref.destination else None,
                    reason=f"Learned preference (confidence: {pref.confidence:.0%})",
                    confidence=pref.confidence,
                    classification=classification,
                    requires_confirmation=pref.confidence < 0.95,
                )

        # Match against rules
        matched_rule = self._match_rule(path, classification)
        if matched_rule:
            dest = self._resolve_destination(matched_rule, path)
            requires_confirm = matched_rule.confirmation_level != ConfirmationLevel.NEVER

            return OrganizationDecision(
                file_path=path,
                rule_id=matched_rule.id,
                action=matched_rule.action,
                destination=dest,
                reason=f"Matched rule: {matched_rule.name}",
                confidence=0.8,
                classification=classification,
                requires_confirmation=requires_confirm,
            )

        # Default: ask user for unknown files
        return OrganizationDecision(
            file_path=path,
            rule_id=None,
            action=OrganizationAction.ASK_USER,
            destination=None,
            reason="No matching rule - needs user input",
            confidence=0.0,
            classification=classification,
            requires_confirmation=True,
        )

    def _get_preference_key(self, path: Path) -> str:
        """Generate preference key for a file."""
        # Use extension and parent folder as key
        return f"{path.suffix.lower()}:{path.parent.name.lower()}"

    def _match_rule(self, path: Path, classification: FileClassification) -> Optional[OrganizationRule]:
        """Find matching rule for a file."""
        # Sort rules by priority
        sorted_rules = sorted(
            [r for r in self.rules if r.enabled],
            key=lambda r: -r.priority
        )

        for rule in sorted_rules:
            if self._rule_matches(rule, path, classification):
                return rule

        return None

    def _rule_matches(self, rule: OrganizationRule, path: Path, classification: FileClassification) -> bool:
        """Check if a rule matches a file."""
        # Check extension
        if rule.extensions:
            if path.suffix.lower() not in [e.lower() for e in rule.extensions]:
                return False

        # Check name patterns
        if rule.name_patterns:
            import fnmatch
            matched = False
            for pattern in rule.name_patterns:
                if fnmatch.fnmatch(path.name.lower(), pattern.lower()):
                    matched = True
                    break
            if not matched:
                return False

        # Check source folders
        if rule.source_folders:
            in_source = False
            for folder in rule.source_folders:
                try:
                    path.relative_to(folder)
                    in_source = True
                    break
                except ValueError:
                    continue
            if not in_source:
                return False

        # Check size
        try:
            size = path.stat().st_size
            if rule.min_size_bytes and size < rule.min_size_bytes:
                return False
            if rule.max_size_bytes and size > rule.max_size_bytes:
                return False
        except Exception:
            pass

        # Check file origin
        if rule.file_origin and classification.origin != rule.file_origin:
            return False

        return True

    def _resolve_destination(self, rule: OrganizationRule, path: Path) -> Optional[Path]:
        """Resolve the destination path for a rule."""
        if not rule.destination:
            return None

        dest = Path(rule.destination)

        # Apply subfolder pattern
        if rule.subfolder_pattern:
            try:
                stat = path.stat()
                mtime = datetime.fromtimestamp(stat.st_mtime)

                subfolder = rule.subfolder_pattern.format(
                    year=mtime.year,
                    month=f"{mtime.month:02d}",
                    day=f"{mtime.day:02d}",
                    ext=path.suffix[1:] if path.suffix else "other",
                )
                dest = dest / subfolder
            except Exception:
                pass

        return dest / path.name

    def _handle_confirmation_needed(self, decision: OrganizationDecision) -> None:
        """Handle a decision that needs user confirmation."""
        self.pending_decisions[str(decision.file_path)] = decision

        # Call confirmation callback if provided
        if self.confirmation_callback:
            try:
                confirmed = self.confirmation_callback(decision)
                self.confirm_decision(decision.file_path, confirmed)
            except Exception as e:
                logger.error(f"Confirmation callback error: {e}")

    def confirm_decision(self, path: Path, confirmed: bool, feedback: Optional[str] = None) -> None:
        """User confirms or rejects a decision."""
        key = str(path.resolve())
        decision = self.pending_decisions.get(key)

        if not decision:
            logger.warning(f"No pending decision for {path}")
            return

        decision.user_confirmed = confirmed
        decision.user_feedback = feedback

        # Update preferences based on feedback
        pref_key = self._get_preference_key(path)
        if pref_key not in self.preferences:
            self.preferences[pref_key] = UserPreference(
                pattern=pref_key,
                action=decision.action,
                destination=str(decision.destination) if decision.destination else None,
            )

        pref = self.preferences[pref_key]
        if confirmed:
            pref.times_confirmed += 1
            pref.last_used = datetime.now()
        else:
            pref.times_rejected += 1

        # Execute if confirmed
        if confirmed:
            self._execute_decision(decision)
        else:
            logger.info(f"User rejected organization of {path}")

        # Remove from pending
        del self.pending_decisions[key]

        # Save preferences
        self._save_config()

    def _execute_decision(self, decision: OrganizationDecision) -> bool:
        """Execute an organization decision."""
        try:
            if decision.action == OrganizationAction.IGNORE:
                logger.debug(f"Ignoring {decision.file_path}: {decision.reason}")
                return True

            if decision.action == OrganizationAction.ASK_USER:
                self._handle_confirmation_needed(decision)
                return True

            if decision.action == OrganizationAction.MOVE:
                return self._execute_move(decision)

            if decision.action == OrganizationAction.COPY:
                return self._execute_copy(decision)

            if decision.action == OrganizationAction.DELETE:
                return self._execute_delete(decision)

            if decision.action == OrganizationAction.ARCHIVE:
                return self._execute_archive(decision)

            logger.warning(f"Unknown action: {decision.action}")
            return False

        except Exception as e:
            logger.error(f"Error executing decision: {e}")
            return False

    def _execute_move(self, decision: OrganizationDecision) -> bool:
        """Execute a move operation."""
        if not decision.destination:
            return False

        src = decision.file_path
        dest = decision.destination

        try:
            # Ensure destination directory exists
            dest.parent.mkdir(parents=True, exist_ok=True)

            # Handle name conflicts
            if dest.exists():
                base = dest.stem
                ext = dest.suffix
                counter = 1
                while dest.exists():
                    dest = dest.parent / f"{base}_{counter}{ext}"
                    counter += 1

            # Store original for undo
            decision.original_path = src

            # Move the file
            shutil.move(str(src), str(dest))

            decision.executed_at = datetime.now()
            decision.undo_deadline = datetime.now() + timedelta(hours=24)

            # Add to history and undo stack
            self.decision_history.append(decision)
            self.undo_stack.append(decision)

            logger.info(f"Moved {src} -> {dest}")
            return True

        except Exception as e:
            logger.error(f"Move failed: {e}")
            return False

    def _execute_copy(self, decision: OrganizationDecision) -> bool:
        """Execute a copy operation."""
        if not decision.destination:
            return False

        src = decision.file_path
        dest = decision.destination

        try:
            dest.parent.mkdir(parents=True, exist_ok=True)

            # Handle name conflicts
            if dest.exists():
                base = dest.stem
                ext = dest.suffix
                counter = 1
                while dest.exists():
                    dest = dest.parent / f"{base}_{counter}{ext}"
                    counter += 1

            shutil.copy2(str(src), str(dest))

            decision.executed_at = datetime.now()
            self.decision_history.append(decision)

            logger.info(f"Copied {src} -> {dest}")
            return True

        except Exception as e:
            logger.error(f"Copy failed: {e}")
            return False

    def _execute_delete(self, decision: OrganizationDecision) -> bool:
        """Execute a delete operation (moves to trash)."""
        try:
            # Move to trash instead of permanent delete
            import send2trash
            send2trash.send2trash(str(decision.file_path))

            decision.executed_at = datetime.now()
            self.decision_history.append(decision)

            logger.info(f"Deleted (to trash) {decision.file_path}")
            return True

        except ImportError:
            # Fallback: move to a trash folder
            trash_dir = Path.home() / ".nexusfs" / "trash"
            trash_dir.mkdir(parents=True, exist_ok=True)

            dest = trash_dir / f"{decision.file_path.name}_{int(time.time())}"
            shutil.move(str(decision.file_path), str(dest))

            decision.original_path = decision.file_path
            decision.destination = dest
            decision.executed_at = datetime.now()
            decision.undo_deadline = datetime.now() + timedelta(days=30)

            self.decision_history.append(decision)
            self.undo_stack.append(decision)

            logger.info(f"Deleted (to trash folder) {decision.file_path}")
            return True

        except Exception as e:
            logger.error(f"Delete failed: {e}")
            return False

    def _execute_archive(self, decision: OrganizationDecision) -> bool:
        """Execute an archive/extract operation."""
        try:
            import zipfile
            import tarfile

            src = decision.file_path

            # Determine extract location
            if decision.destination:
                extract_dir = decision.destination.parent
            else:
                extract_dir = src.parent / src.stem

            extract_dir.mkdir(parents=True, exist_ok=True)

            # Extract based on type
            if src.suffix.lower() == ".zip":
                with zipfile.ZipFile(src, 'r') as zf:
                    zf.extractall(extract_dir)
            elif src.suffix.lower() in [".tar", ".gz", ".tgz", ".bz2"]:
                with tarfile.open(src, 'r:*') as tf:
                    tf.extractall(extract_dir)
            elif src.suffix.lower() in [".7z", ".rar"]:
                logger.warning(f"7z/rar extraction requires additional tools: {src}")
                return False
            else:
                logger.warning(f"Unknown archive format: {src}")
                return False

            decision.executed_at = datetime.now()
            self.decision_history.append(decision)

            logger.info(f"Extracted {src} -> {extract_dir}")
            return True

        except Exception as e:
            logger.error(f"Archive extraction failed: {e}")
            return False

    def undo_last(self) -> Optional[OrganizationDecision]:
        """Undo the last organization action."""
        if not self.undo_stack:
            logger.info("Nothing to undo")
            return None

        decision = self.undo_stack.pop()

        # Check if undo is still valid
        if decision.undo_deadline and datetime.now() > decision.undo_deadline:
            logger.warning("Undo deadline has passed")
            return None

        if not decision.can_undo:
            logger.warning("This action cannot be undone")
            return None

        try:
            if decision.action == OrganizationAction.MOVE:
                # Move back to original location
                if decision.original_path and decision.destination:
                    if decision.destination.exists():
                        decision.original_path.parent.mkdir(parents=True, exist_ok=True)
                        shutil.move(str(decision.destination), str(decision.original_path))
                        logger.info(f"Undone: {decision.destination} -> {decision.original_path}")
                        return decision

            elif decision.action == OrganizationAction.DELETE:
                # Restore from trash
                if decision.destination and decision.original_path:
                    if decision.destination.exists():
                        decision.original_path.parent.mkdir(parents=True, exist_ok=True)
                        shutil.move(str(decision.destination), str(decision.original_path))
                        logger.info(f"Restored from trash: {decision.original_path}")
                        return decision

        except Exception as e:
            logger.error(f"Undo failed: {e}")

        return None

    def get_pending_decisions(self) -> List[OrganizationDecision]:
        """Get all pending decisions awaiting user confirmation."""
        return list(self.pending_decisions.values())

    def get_statistics(self) -> Dict[str, Any]:
        """Get organizer statistics."""
        return {
            "watched_folders": len(self._watched_folders),
            "rules_count": len(self.rules),
            "preferences_learned": len(self.preferences),
            "pending_decisions": len(self.pending_decisions),
            "history_count": len(self.decision_history),
            "undo_stack_size": len(self.undo_stack),
            "running": self._running,
        }


# Global organizer instance
_organizer: Optional[RealtimeOrganizer] = None


def get_organizer() -> RealtimeOrganizer:
    """Get global realtime organizer instance."""
    global _organizer
    if _organizer is None:
        _organizer = RealtimeOrganizer()
    return _organizer


def start_organizing(folders: List[Path]) -> None:
    """Convenience function to start organizing folders."""
    organizer = get_organizer()
    for folder in folders:
        organizer.add_watch_folder(folder)
    organizer.start()


def stop_organizing() -> None:
    """Stop the organizer."""
    if _organizer:
        _organizer.stop()
