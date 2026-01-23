"""
Enterprise Logging Configuration

Real-time logging with multiple outputs:
- Console (colored)
- File (rotating)
- JSON (structured)
- Remote (optional)
"""

from __future__ import annotations

import sys
import json
from pathlib import Path
from datetime import datetime
from typing import Optional, Dict, Any, Callable
from dataclasses import dataclass, field
from enum import Enum
import threading
import queue
import atexit

from loguru import logger


class LogLevel(Enum):
    """Log levels with numeric values."""
    TRACE = 5
    DEBUG = 10
    INFO = 20
    SUCCESS = 25
    WARNING = 30
    ERROR = 40
    CRITICAL = 50


@dataclass
class LogConfig:
    """Logging configuration."""
    console_enabled: bool = True
    console_level: LogLevel = LogLevel.INFO
    console_format: str = "<green>{time:HH:mm:ss}</green> | <level>{level: <8}</level> | <cyan>{name}</cyan>:<cyan>{function}</cyan>:<cyan>{line}</cyan> - <level>{message}</level>"

    file_enabled: bool = True
    file_level: LogLevel = LogLevel.DEBUG
    file_path: Path = field(default_factory=lambda: Path("D:/NexusFS/logs/nexus.log"))
    file_rotation: str = "10 MB"
    file_retention: str = "7 days"
    file_compression: str = "zip"

    json_enabled: bool = True
    json_path: Path = field(default_factory=lambda: Path("D:/NexusFS/logs/nexus.json"))

    # Real-time callbacks
    realtime_callbacks: list = field(default_factory=list)


class RealTimeLogHandler:
    """
    Handler for real-time log streaming.

    Supports:
    - WebSocket streaming
    - UI callbacks
    - Event aggregation
    """

    def __init__(self):
        self._callbacks: list[Callable[[Dict[str, Any]], None]] = []
        self._queue: queue.Queue = queue.Queue()
        self._running = False
        self._thread: Optional[threading.Thread] = None
        self._lock = threading.Lock()

    def add_callback(self, callback: Callable[[Dict[str, Any]], None]) -> None:
        """Add a real-time log callback."""
        with self._lock:
            self._callbacks.append(callback)

    def remove_callback(self, callback: Callable[[Dict[str, Any]], None]) -> None:
        """Remove a callback."""
        with self._lock:
            if callback in self._callbacks:
                self._callbacks.remove(callback)

    def start(self) -> None:
        """Start the real-time handler."""
        if self._running:
            return
        self._running = True
        self._thread = threading.Thread(target=self._process_queue, daemon=True)
        self._thread.start()

    def stop(self) -> None:
        """Stop the handler."""
        self._running = False
        if self._thread:
            self._thread.join(timeout=1.0)

    def emit(self, record: Dict[str, Any]) -> None:
        """Emit a log record to all callbacks."""
        if self._running:
            self._queue.put(record)

    def _process_queue(self) -> None:
        """Process log records from queue."""
        while self._running:
            try:
                record = self._queue.get(timeout=0.1)
                with self._lock:
                    for callback in self._callbacks:
                        try:
                            callback(record)
                        except Exception:
                            pass  # Don't let callback errors break logging
            except queue.Empty:
                continue


# Global real-time handler
_realtime_handler = RealTimeLogHandler()


def _serialize_record(record) -> Dict[str, Any]:
    """Serialize a loguru record to dict."""
    return {
        "timestamp": record["time"].isoformat(),
        "level": record["level"].name,
        "level_no": record["level"].no,
        "message": record["message"],
        "module": record["name"],
        "function": record["function"],
        "line": record["line"],
        "file": record["file"].path if record["file"] else None,
        "exception": str(record["exception"]) if record["exception"] else None,
        "extra": dict(record["extra"]),
    }


def _realtime_sink(message):
    """Sink for real-time logging."""
    record = message.record
    _realtime_handler.emit(_serialize_record(record))


def setup_logging(config: Optional[LogConfig] = None) -> None:
    """
    Configure enterprise logging.

    Args:
        config: Logging configuration (uses defaults if None)
    """
    if config is None:
        config = LogConfig()

    # Remove default handler
    logger.remove()

    # Console handler
    if config.console_enabled:
        logger.add(
            sys.stderr,
            format=config.console_format,
            level=config.console_level.name,
            colorize=True,
        )

    # File handler
    if config.file_enabled:
        config.file_path.parent.mkdir(parents=True, exist_ok=True)
        logger.add(
            str(config.file_path),
            format="{time:YYYY-MM-DD HH:mm:ss.SSS} | {level: <8} | {name}:{function}:{line} | {message}",
            level=config.file_level.name,
            rotation=config.file_rotation,
            retention=config.file_retention,
            compression=config.file_compression,
            enqueue=True,  # Thread-safe
        )

    # JSON handler
    if config.json_enabled:
        config.json_path.parent.mkdir(parents=True, exist_ok=True)
        logger.add(
            str(config.json_path),
            format="{message}",
            level=config.file_level.name,
            rotation=config.file_rotation,
            retention=config.file_retention,
            serialize=True,
            enqueue=True,
        )

    # Real-time handler
    logger.add(_realtime_sink, level="DEBUG")
    _realtime_handler.start()

    # Register cleanup
    atexit.register(_realtime_handler.stop)

    logger.info("Logging initialized", config=str(config))


def get_logger(name: str = "nexus"):
    """
    Get a contextualized logger.

    Args:
        name: Logger name/context

    Returns:
        Contextualized logger instance
    """
    return logger.bind(name=name)


def add_realtime_callback(callback: Callable[[Dict[str, Any]], None]) -> None:
    """
    Add a callback for real-time log events.

    Useful for:
    - UI log viewers
    - WebSocket streaming
    - Metrics collection

    Args:
        callback: Function that receives log records as dicts
    """
    _realtime_handler.add_callback(callback)


def remove_realtime_callback(callback: Callable[[Dict[str, Any]], None]) -> None:
    """Remove a real-time callback."""
    _realtime_handler.remove_callback(callback)


# Convenience decorators
def log_function_call(func):
    """Decorator to log function calls."""
    import functools

    @functools.wraps(func)
    def wrapper(*args, **kwargs):
        func_logger = get_logger(func.__module__)
        func_logger.debug(f"Calling {func.__name__}", args=str(args)[:100], kwargs=str(kwargs)[:100])
        try:
            result = func(*args, **kwargs)
            func_logger.debug(f"Completed {func.__name__}", result_type=type(result).__name__)
            return result
        except Exception as e:
            func_logger.exception(f"Error in {func.__name__}: {e}")
            raise

    return wrapper


def log_async_function_call(func):
    """Decorator to log async function calls."""
    import functools
    import asyncio

    @functools.wraps(func)
    async def wrapper(*args, **kwargs):
        func_logger = get_logger(func.__module__)
        func_logger.debug(f"Calling async {func.__name__}", args=str(args)[:100], kwargs=str(kwargs)[:100])
        try:
            result = await func(*args, **kwargs)
            func_logger.debug(f"Completed async {func.__name__}", result_type=type(result).__name__)
            return result
        except Exception as e:
            func_logger.exception(f"Error in async {func.__name__}: {e}")
            raise

    return wrapper


# Performance logging context
class LogPerformance:
    """Context manager for performance logging."""

    def __init__(self, operation: str, logger_name: str = "performance"):
        self.operation = operation
        self.logger = get_logger(logger_name)
        self.start_time: Optional[datetime] = None

    def __enter__(self):
        self.start_time = datetime.now()
        self.logger.debug(f"Starting: {self.operation}")
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        duration = 0.0
        if self.start_time is not None:
            duration = (datetime.now() - self.start_time).total_seconds() * 1000
        if exc_type:
            self.logger.error(f"Failed: {self.operation}", duration_ms=duration, error=str(exc_val))
        else:
            self.logger.info(f"Completed: {self.operation}", duration_ms=duration)
        return False
