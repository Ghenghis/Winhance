"""
NexusFS Core Module

Enterprise-grade AI-powered file system management.
"""

from nexus_ai.core.agents import (
    Agent,
    AgentOrchestrator,
    AgentStatus,
    AgentTask,
    AgentType,
    TaskPriority,
    get_orchestrator,
    quick_task,
)
from nexus_ai.core.ai_providers import (
    AIConfig,
    AIMessage,
    AIProvider,
    AIProviderManager,
    AIResponse,
    ProviderType,
    get_ai_manager,
)
from nexus_ai.core.backup_system import (
    BackupConfig,
    BackupManager,
    BackupRecord,
    BackupStatus,
    BackupType,
    RestorePoint,
    create_restore_point_before,
    get_backup_manager,
)
from nexus_ai.core.doc_automation import (
    CodeAnalyzer,
    DiagramGenerator,
    DocGenerator,
    DocumentationAgent,
    get_doc_agent,
)
from nexus_ai.core.gpu_accelerator import (
    GPUAccelerator,
    GPUConfig,
    GPUInfo,
    check_gpu_status,
    get_gpu_accelerator,
)
from nexus_ai.core.logging_config import (
    LogConfig,
    LogLevel,
    LogPerformance,
    add_realtime_callback,
    get_logger,
    log_async_function_call,
    log_function_call,
    remove_realtime_callback,
    setup_logging,
)

__all__ = [
    # Logging
    "setup_logging",
    "get_logger",
    "add_realtime_callback",
    "remove_realtime_callback",
    "LogConfig",
    "LogLevel",
    "LogPerformance",
    "log_function_call",
    "log_async_function_call",
    # AI Providers
    "AIProviderManager",
    "AIProvider",
    "AIConfig",
    "AIMessage",
    "AIResponse",
    "ProviderType",
    "get_ai_manager",
    # Agents
    "AgentOrchestrator",
    "Agent",
    "AgentTask",
    "AgentType",
    "AgentStatus",
    "TaskPriority",
    "get_orchestrator",
    "quick_task",
    # GPU
    "GPUAccelerator",
    "GPUConfig",
    "GPUInfo",
    "get_gpu_accelerator",
    "check_gpu_status",
    # Backup
    "BackupManager",
    "BackupConfig",
    "BackupRecord",
    "BackupType",
    "BackupStatus",
    "RestorePoint",
    "get_backup_manager",
    "create_restore_point_before",
    # Documentation
    "DocumentationAgent",
    "DocGenerator",
    "CodeAnalyzer",
    "DiagramGenerator",
    "get_doc_agent",
]
