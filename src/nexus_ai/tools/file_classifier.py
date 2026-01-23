"""
Intelligent File Classification System

Game-changing features:
- Windows System File Detection
- User-Created vs App-Generated identification
- Safe-to-Move analysis
- Installation footprint mapping
- Real-time safety ratings
"""

from __future__ import annotations

import os
import re
import winreg
from dataclasses import dataclass, field
from datetime import datetime
from enum import Enum
from pathlib import Path
from typing import Dict, List, Optional, Set, Tuple
from concurrent.futures import ThreadPoolExecutor
import ctypes
import json

from nexus_ai.core.logging_config import get_logger

logger = get_logger("file_classifier")


class SafetyLevel(Enum):
    """File safety levels for move/delete operations."""
    CRITICAL = "critical"      # System critical - NEVER touch
    PROTECTED = "protected"    # Windows protected - requires admin
    INSTALLED = "installed"    # Part of installed app - warn user
    CAUTIOUS = "cautious"      # Might be important - ask user
    SAFE = "safe"              # User file - safe to move
    TEMPORARY = "temporary"    # Temp/cache - safe to delete


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


@dataclass
class FileClassification:
    """Complete classification of a file."""
    path: Path
    safety_level: SafetyLevel
    origin: FileOrigin
    confidence: float  # 0.0 to 1.0

    # Detailed info
    is_system_file: bool = False
    is_protected: bool = False
    is_hidden: bool = False
    is_readonly: bool = False

    # App association
    associated_app: Optional[str] = None
    app_install_path: Optional[str] = None
    registry_references: List[str] = field(default_factory=list)

    # Safety info
    safe_to_move: bool = True
    safe_to_delete: bool = True
    requires_admin: bool = False
    requires_confirmation: bool = False
    warning_message: Optional[str] = None

    # Recommendations
    suggested_action: Optional[str] = None
    alternative_location: Optional[str] = None


@dataclass
class InstalledApp:
    """Information about an installed application."""
    name: str
    install_path: Path
    publisher: Optional[str] = None
    version: Optional[str] = None
    install_date: Optional[datetime] = None
    uninstall_string: Optional[str] = None
    registry_key: Optional[str] = None
    file_extensions: List[str] = field(default_factory=list)
    known_folders: List[Path] = field(default_factory=list)


class WindowsSystemDetector:
    """
    Detects Windows system files and their safety levels.

    Features:
    - System32/SysWOW64 detection
    - Windows protected file detection
    - Driver file identification
    - Windows Update component detection
    """

    # Critical Windows paths - NEVER modify
    CRITICAL_PATHS = {
        Path(os.environ.get("SYSTEMROOT", "C:\\Windows")),
        Path(os.environ.get("SYSTEMROOT", "C:\\Windows")) / "System32",
        Path(os.environ.get("SYSTEMROOT", "C:\\Windows")) / "SysWOW64",
        Path(os.environ.get("SYSTEMROOT", "C:\\Windows")) / "WinSxS",
        Path(os.environ.get("SYSTEMROOT", "C:\\Windows")) / "assembly",
    }

    # Protected Windows folders
    PROTECTED_FOLDERS = {
        "System32", "SysWOW64", "WinSxS", "assembly", "Boot",
        "Fonts", "Globalization", "IME", "Installer", "Logs",
        "Microsoft.NET", "Panther", "PolicyDefinitions", "Prefetch",
        "Registration", "Resources", "SchCache", "security",
        "ServiceProfiles", "servicing", "Setup", "SoftwareDistribution",
        "System", "SystemResources", "TAPI", "Tasks", "Temp", "tracing",
        "WaaS", "Web", "WinSxS", "Vss",
    }

    # System file extensions
    SYSTEM_EXTENSIONS = {
        ".sys", ".dll", ".exe", ".drv", ".ocx", ".cpl", ".msc",
        ".inf", ".cat", ".mum", ".manifest",
    }

    # Driver paths
    DRIVER_PATHS = {
        Path(os.environ.get("SYSTEMROOT", "C:\\Windows")) / "System32" / "drivers",
        Path(os.environ.get("SYSTEMROOT", "C:\\Windows")) / "System32" / "DriverStore",
    }

    def __init__(self):
        self.windows_root = Path(os.environ.get("SYSTEMROOT", "C:\\Windows"))
        self.program_files = Path(os.environ.get("PROGRAMFILES", "C:\\Program Files"))
        self.program_files_x86 = Path(os.environ.get("PROGRAMFILES(X86)", "C:\\Program Files (x86)"))
        self._system_file_cache: Dict[str, bool] = {}

    def is_system_path(self, path: Path) -> bool:
        """Check if path is within Windows system directories."""
        try:
            path = path.resolve()
            for critical in self.CRITICAL_PATHS:
                try:
                    path.relative_to(critical)
                    return True
                except ValueError:
                    continue
            return False
        except Exception:
            return False

    def is_protected_folder(self, path: Path) -> bool:
        """Check if path is in a protected Windows folder."""
        try:
            parts = path.parts
            for part in parts:
                if part in self.PROTECTED_FOLDERS:
                    return True
            return False
        except Exception:
            return False

    def is_driver_file(self, path: Path) -> bool:
        """Check if file is a driver."""
        try:
            path = path.resolve()
            # Check by location
            for driver_path in self.DRIVER_PATHS:
                try:
                    path.relative_to(driver_path)
                    return True
                except ValueError:
                    continue
            # Check by extension
            if path.suffix.lower() in {".sys", ".drv"}:
                return True
            return False
        except Exception:
            return False

    def is_windows_component(self, path: Path) -> bool:
        """Check if file is a Windows component."""
        try:
            path = path.resolve()

            # Check if in Windows directory
            try:
                path.relative_to(self.windows_root)
                return True
            except ValueError:
                pass

            # Check for Windows digital signature
            if path.suffix.lower() in self.SYSTEM_EXTENSIONS:
                # Could verify digital signature here
                return self._check_windows_signature(path)

            return False
        except Exception:
            return False

    def _check_windows_signature(self, path: Path) -> bool:
        """Check if file has Windows digital signature."""
        # Simplified check - in production would use WinVerifyTrust API
        try:
            if path.exists() and path.suffix.lower() in self.SYSTEM_EXTENSIONS:
                # Check if in system paths
                return self.is_system_path(path)
        except Exception:
            pass
        return False

    def get_system_safety_level(self, path: Path) -> Tuple[SafetyLevel, str]:
        """
        Determine safety level for a system file.

        Returns:
            Tuple of (SafetyLevel, reason_message)
        """
        try:
            path = path.resolve()

            # Critical system files
            if self.is_driver_file(path):
                return SafetyLevel.CRITICAL, "Driver file - modifying could crash system"

            if path.suffix.lower() == ".sys":
                return SafetyLevel.CRITICAL, "System driver file"

            # System32/SysWOW64 files
            sys32 = self.windows_root / "System32"
            syswow = self.windows_root / "SysWOW64"

            try:
                path.relative_to(sys32)
                return SafetyLevel.CRITICAL, "Windows System32 file - essential for Windows"
            except ValueError:
                pass

            try:
                path.relative_to(syswow)
                return SafetyLevel.CRITICAL, "Windows SysWOW64 file - essential for 32-bit apps"
            except ValueError:
                pass

            # Other Windows files
            if self.is_windows_component(path):
                return SafetyLevel.PROTECTED, "Windows component file"

            # Protected folders
            if self.is_protected_folder(path):
                return SafetyLevel.PROTECTED, "File in protected Windows folder"

            return SafetyLevel.SAFE, "Not a system file"

        except Exception as e:
            logger.warning(f"Error checking system safety: {e}")
            return SafetyLevel.CAUTIOUS, "Unable to verify - proceed with caution"


class InstalledAppDetector:
    """
    Detects installed applications and their file footprints.

    Features:
    - Registry scanning for installed apps
    - File association detection
    - Install footprint mapping
    - Dependency tracking
    """

    UNINSTALL_KEYS = [
        r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        r"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
    ]

    def __init__(self):
        self._app_cache: Dict[str, InstalledApp] = {}
        self._path_to_app: Dict[str, str] = {}
        self._loaded = False

    def load_installed_apps(self) -> Dict[str, InstalledApp]:
        """Load all installed applications from registry."""
        if self._loaded:
            return self._app_cache

        apps = {}

        for hive in [winreg.HKEY_LOCAL_MACHINE, winreg.HKEY_CURRENT_USER]:
            for key_path in self.UNINSTALL_KEYS:
                try:
                    key = winreg.OpenKey(hive, key_path)
                    i = 0
                    while True:
                        try:
                            subkey_name = winreg.EnumKey(key, i)
                            subkey = winreg.OpenKey(key, subkey_name)

                            app = self._parse_app_key(subkey, subkey_name)
                            if app and app.name:
                                apps[app.name.lower()] = app

                                # Map install path to app
                                if app.install_path:
                                    self._path_to_app[str(app.install_path).lower()] = app.name

                            winreg.CloseKey(subkey)
                            i += 1
                        except WindowsError:
                            break
                    winreg.CloseKey(key)
                except WindowsError:
                    continue

        self._app_cache = apps
        self._loaded = True
        logger.info(f"Loaded {len(apps)} installed applications")
        return apps

    def _parse_app_key(self, key, key_name: str) -> Optional[InstalledApp]:
        """Parse registry key to InstalledApp."""
        try:
            name = self._get_reg_value(key, "DisplayName")
            if not name:
                return None

            install_path = self._get_reg_value(key, "InstallLocation")
            if not install_path:
                # Try to get from UninstallString
                uninstall = self._get_reg_value(key, "UninstallString")
                if uninstall:
                    # Extract path from uninstall string
                    match = re.search(r'([A-Za-z]:\\[^"]+)', uninstall)
                    if match:
                        install_path = str(Path(match.group(1)).parent)

            return InstalledApp(
                name=name,
                install_path=Path(install_path) if install_path else None,
                publisher=self._get_reg_value(key, "Publisher"),
                version=self._get_reg_value(key, "DisplayVersion"),
                uninstall_string=self._get_reg_value(key, "UninstallString"),
                registry_key=key_name,
            )
        except Exception:
            return None

    def _get_reg_value(self, key, name: str) -> Optional[str]:
        """Get registry value, return None if not found."""
        try:
            value, _ = winreg.QueryValueEx(key, name)
            return str(value) if value else None
        except WindowsError:
            return None

    def find_app_for_path(self, path: Path) -> Optional[InstalledApp]:
        """Find which installed app owns this path."""
        if not self._loaded:
            self.load_installed_apps()

        try:
            path = path.resolve()
            path_str = str(path).lower()

            # Direct match
            for app_path, app_name in self._path_to_app.items():
                if path_str.startswith(app_path):
                    return self._app_cache.get(app_name.lower())

            # Check Program Files
            for program_dir in [
                Path(os.environ.get("PROGRAMFILES", "C:\\Program Files")),
                Path(os.environ.get("PROGRAMFILES(X86)", "C:\\Program Files (x86)")),
            ]:
                try:
                    rel_path = path.relative_to(program_dir)
                    app_folder = rel_path.parts[0] if rel_path.parts else None
                    if app_folder:
                        # Search for app by folder name
                        for name, app in self._app_cache.items():
                            if app_folder.lower() in name.lower():
                                return app
                except ValueError:
                    continue

            return None
        except Exception:
            return None

    def get_app_footprint(self, app: InstalledApp) -> Dict[str, List[Path]]:
        """Get complete file footprint of an application."""
        footprint = {
            "install_files": [],
            "appdata_local": [],
            "appdata_roaming": [],
            "programdata": [],
            "temp_files": [],
            "registry_entries": [],
        }

        if app.install_path and app.install_path.exists():
            for f in app.install_path.rglob("*"):
                if f.is_file():
                    footprint["install_files"].append(f)

        # Check AppData
        appdata_local = Path(os.environ.get("LOCALAPPDATA", ""))
        appdata_roaming = Path(os.environ.get("APPDATA", ""))
        programdata = Path(os.environ.get("PROGRAMDATA", "C:\\ProgramData"))

        for name_variant in [app.name, app.name.replace(" ", ""), app.publisher or ""]:
            if not name_variant:
                continue

            for check_path in [
                appdata_local / name_variant,
                appdata_roaming / name_variant,
                programdata / name_variant,
            ]:
                if check_path.exists():
                    for f in check_path.rglob("*"):
                        if f.is_file():
                            if "local" in str(check_path).lower():
                                footprint["appdata_local"].append(f)
                            elif "roaming" in str(check_path).lower():
                                footprint["appdata_roaming"].append(f)
                            else:
                                footprint["programdata"].append(f)

        return footprint


class UserFileDetector:
    """
    Identifies user-created vs app-generated files.

    Features:
    - User folder detection
    - Created vs downloaded distinction
    - Project file identification
    - Personal content detection
    """

    # User-created content indicators
    USER_CONTENT_EXTENSIONS = {
        ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".txt", ".rtf", ".pdf", ".odt", ".ods", ".odp",
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff",
        ".mp3", ".wav", ".flac", ".m4a", ".aac",
        ".mp4", ".mov", ".avi", ".mkv", ".wmv",
        ".psd", ".ai", ".sketch", ".fig",
    }

    # Development project indicators
    PROJECT_INDICATORS = {
        "package.json", "requirements.txt", "Cargo.toml", "go.mod",
        ".git", ".gitignore", "README.md", "setup.py", "pyproject.toml",
        "CMakeLists.txt", "Makefile", ".sln", ".csproj",
    }

    # App-generated patterns
    APP_GENERATED_PATTERNS = [
        r".*\.tmp$",
        r".*\.log$",
        r".*\.cache$",
        r"~\$.*",  # Office temp files
        r"Thumbs\.db$",
        r"desktop\.ini$",
        r".*\.lnk$",  # Shortcuts
    ]

    def __init__(self):
        self.user_home = Path.home()
        self.user_folders = self._get_user_folders()
        self._app_generated_re = [re.compile(p, re.IGNORECASE) for p in self.APP_GENERATED_PATTERNS]

    def _get_user_folders(self) -> Dict[str, Path]:
        """Get standard user folders."""
        return {
            "documents": self.user_home / "Documents",
            "desktop": self.user_home / "Desktop",
            "downloads": self.user_home / "Downloads",
            "pictures": self.user_home / "Pictures",
            "music": self.user_home / "Music",
            "videos": self.user_home / "Videos",
        }

    def is_user_folder(self, path: Path) -> bool:
        """Check if path is in a user folder."""
        try:
            path = path.resolve()
            for folder in self.user_folders.values():
                try:
                    path.relative_to(folder)
                    return True
                except ValueError:
                    continue
            return False
        except Exception:
            return False

    def is_user_created(self, path: Path) -> Tuple[bool, float]:
        """
        Determine if file was user-created.

        Returns:
            Tuple of (is_user_created, confidence)
        """
        try:
            path = path.resolve()
            confidence = 0.5  # Start neutral

            # Check location
            if self.is_user_folder(path):
                confidence += 0.2

            # Check extension
            if path.suffix.lower() in self.USER_CONTENT_EXTENSIONS:
                confidence += 0.2

            # Check for app-generated patterns
            for pattern in self._app_generated_re:
                if pattern.match(path.name):
                    return False, 0.9

            # Check file metadata
            try:
                stat = path.stat()
                # User files are usually smaller and older
                if stat.st_size < 100 * 1024 * 1024:  # < 100MB
                    confidence += 0.05
            except Exception:
                pass

            return confidence > 0.5, min(confidence, 1.0)

        except Exception:
            return False, 0.0

    def is_project_file(self, path: Path) -> bool:
        """Check if file is part of a development project."""
        try:
            # Check if file itself is a project indicator
            if path.name in self.PROJECT_INDICATORS:
                return True

            # Check parent directories for project indicators
            for parent in path.parents:
                for indicator in self.PROJECT_INDICATORS:
                    if (parent / indicator).exists():
                        return True
                # Don't go too far up
                if parent == self.user_home:
                    break

            return False
        except Exception:
            return False

    def is_downloaded(self, path: Path) -> bool:
        """Check if file was downloaded."""
        try:
            # Check if in Downloads folder
            downloads = self.user_folders.get("downloads")
            if downloads:
                try:
                    path.relative_to(downloads)
                    return True
                except ValueError:
                    pass

            # Check for Zone.Identifier stream (download marker)
            zone_id_path = Path(str(path) + ":Zone.Identifier")
            if zone_id_path.exists():
                return True

            # Check file attributes for downloaded file markers
            # This would check the alternate data stream in production

            return False
        except Exception:
            return False


class FileClassifier:
    """
    Main file classification system combining all detectors.

    Provides comprehensive file safety analysis with:
    - System file detection
    - Installed app association
    - User file identification
    - Safety recommendations
    - Move/delete guidance
    """

    def __init__(self):
        self.system_detector = WindowsSystemDetector()
        self.app_detector = InstalledAppDetector()
        self.user_detector = UserFileDetector()

        # Pre-load installed apps in background
        self._executor = ThreadPoolExecutor(max_workers=2)
        self._executor.submit(self.app_detector.load_installed_apps)

    def classify(self, path: Path) -> FileClassification:
        """
        Fully classify a file with safety analysis.

        Args:
            path: Path to file or folder

        Returns:
            Complete FileClassification with safety info
        """
        try:
            path = path.resolve()

            # Start with basic classification
            classification = FileClassification(
                path=path,
                safety_level=SafetyLevel.SAFE,
                origin=FileOrigin.UNKNOWN,
                confidence=0.5,
            )

            # Check file attributes
            try:
                attrs = ctypes.windll.kernel32.GetFileAttributesW(str(path))
                if attrs != -1:
                    classification.is_hidden = bool(attrs & 2)  # FILE_ATTRIBUTE_HIDDEN
                    classification.is_system_file = bool(attrs & 4)  # FILE_ATTRIBUTE_SYSTEM
                    classification.is_readonly = bool(attrs & 1)  # FILE_ATTRIBUTE_READONLY
            except Exception:
                pass

            # Check if system file
            if self.system_detector.is_system_path(path):
                safety, message = self.system_detector.get_system_safety_level(path)
                classification.safety_level = safety
                classification.origin = FileOrigin.WINDOWS_SYSTEM
                classification.warning_message = message
                classification.safe_to_move = False
                classification.safe_to_delete = False
                classification.requires_admin = True
                classification.requires_confirmation = True
                classification.confidence = 0.95
                return classification

            # Check if driver
            if self.system_detector.is_driver_file(path):
                classification.safety_level = SafetyLevel.CRITICAL
                classification.origin = FileOrigin.DRIVER
                classification.warning_message = "Driver file - do not modify"
                classification.safe_to_move = False
                classification.safe_to_delete = False
                classification.requires_admin = True
                classification.confidence = 0.99
                return classification

            # Check if part of installed app
            app = self.app_detector.find_app_for_path(path)
            if app:
                classification.associated_app = app.name
                classification.app_install_path = str(app.install_path) if app.install_path else None
                classification.safety_level = SafetyLevel.INSTALLED
                classification.origin = FileOrigin.INSTALLED_APP
                classification.warning_message = f"Part of installed application: {app.name}"
                classification.safe_to_move = False
                classification.requires_confirmation = True
                classification.confidence = 0.85
                classification.suggested_action = f"Uninstall {app.name} through Settings if you want to remove this"
                return classification

            # Check if user file
            is_user, user_confidence = self.user_detector.is_user_created(path)
            if is_user:
                classification.origin = FileOrigin.USER_CREATED
                classification.safety_level = SafetyLevel.SAFE
                classification.safe_to_move = True
                classification.safe_to_delete = True
                classification.confidence = user_confidence

                # Check if in project
                if self.user_detector.is_project_file(path):
                    classification.warning_message = "Part of a development project"
                    classification.requires_confirmation = True

                return classification

            # Check if downloaded
            if self.user_detector.is_downloaded(path):
                classification.origin = FileOrigin.USER_DOWNLOADED
                classification.safety_level = SafetyLevel.SAFE
                classification.safe_to_move = True
                classification.safe_to_delete = True
                classification.confidence = 0.8
                return classification

            # Check for temp/cache files
            if self._is_temp_file(path):
                classification.origin = FileOrigin.TEMP_FILE
                classification.safety_level = SafetyLevel.TEMPORARY
                classification.safe_to_move = True
                classification.safe_to_delete = True
                classification.confidence = 0.9
                classification.suggested_action = "Safe to delete to free up space"
                return classification

            # Default: unknown but in user area
            if self.user_detector.is_user_folder(path):
                classification.safety_level = SafetyLevel.SAFE
                classification.origin = FileOrigin.UNKNOWN
                classification.safe_to_move = True
                classification.requires_confirmation = True
                classification.warning_message = "Unknown file in user folder - verify before moving"
            else:
                classification.safety_level = SafetyLevel.CAUTIOUS
                classification.origin = FileOrigin.UNKNOWN
                classification.requires_confirmation = True
                classification.warning_message = "Unknown file origin - proceed with caution"

            return classification

        except Exception as e:
            logger.error(f"Classification error for {path}: {e}")
            return FileClassification(
                path=path,
                safety_level=SafetyLevel.CAUTIOUS,
                origin=FileOrigin.UNKNOWN,
                confidence=0.0,
                warning_message=f"Error during classification: {e}",
                requires_confirmation=True,
            )

    def _is_temp_file(self, path: Path) -> bool:
        """Check if file is a temporary/cache file."""
        temp_indicators = {
            ".tmp", ".temp", ".cache", ".log", ".bak",
        }

        temp_folders = {
            "temp", "tmp", "cache", ".cache", "__pycache__",
            "node_modules", ".npm", ".yarn",
        }

        # Check extension
        if path.suffix.lower() in temp_indicators:
            return True

        # Check folder names
        for part in path.parts:
            if part.lower() in temp_folders:
                return True

        return False

    def batch_classify(self, paths: List[Path]) -> Dict[Path, FileClassification]:
        """Classify multiple files in parallel."""
        results = {}

        with ThreadPoolExecutor(max_workers=8) as executor:
            futures = {executor.submit(self.classify, p): p for p in paths}
            for future in futures:
                path = futures[future]
                try:
                    results[path] = future.result()
                except Exception as e:
                    results[path] = FileClassification(
                        path=path,
                        safety_level=SafetyLevel.CAUTIOUS,
                        origin=FileOrigin.UNKNOWN,
                        confidence=0.0,
                        warning_message=str(e),
                    )

        return results

    def get_safety_summary(self, classifications: Dict[Path, FileClassification]) -> Dict[str, any]:
        """Get summary of safety levels from classifications."""
        summary = {
            "total": len(classifications),
            "by_safety": {level.value: 0 for level in SafetyLevel},
            "by_origin": {origin.value: 0 for origin in FileOrigin},
            "safe_to_move": 0,
            "safe_to_delete": 0,
            "requires_confirmation": 0,
            "warnings": [],
        }

        for path, clf in classifications.items():
            summary["by_safety"][clf.safety_level.value] += 1
            summary["by_origin"][clf.origin.value] += 1

            if clf.safe_to_move:
                summary["safe_to_move"] += 1
            if clf.safe_to_delete:
                summary["safe_to_delete"] += 1
            if clf.requires_confirmation:
                summary["requires_confirmation"] += 1
            if clf.warning_message:
                summary["warnings"].append({
                    "path": str(path),
                    "message": clf.warning_message,
                    "level": clf.safety_level.value,
                })

        return summary


# Global classifier instance
_classifier: Optional[FileClassifier] = None


def get_classifier() -> FileClassifier:
    """Get global file classifier instance."""
    global _classifier
    if _classifier is None:
        _classifier = FileClassifier()
    return _classifier


def classify_file(path: Path) -> FileClassification:
    """Convenience function to classify a single file."""
    return get_classifier().classify(path)


def is_safe_to_move(path: Path) -> Tuple[bool, str]:
    """
    Quick check if file is safe to move.

    Returns:
        Tuple of (is_safe, reason_message)
    """
    clf = classify_file(path)
    return clf.safe_to_move, clf.warning_message or "Safe to move"


def is_safe_to_delete(path: Path) -> Tuple[bool, str]:
    """
    Quick check if file is safe to delete.

    Returns:
        Tuple of (is_safe, reason_message)
    """
    clf = classify_file(path)
    return clf.safe_to_delete, clf.warning_message or "Safe to delete"
