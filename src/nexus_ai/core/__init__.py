"""
NexusFS Core Module

Enterprise-grade AI-powered file system management.
"""

from nexus_ai.core.logging_config import (
    setup_logging,
    get_logger,
    add_realtime_callback,
    remove_realtime_callback,
    LogConfig,
    LogLevel,
    LogPerformance,
    log_function_call,
    log_async_function_call,
)
from nexus_ai.core.ai_providers import (
    AIProviderManager,
    AIProvider,
    AIConfig,
    AIMessage,
    AIResponse,
    ProviderType,
    get_ai_manager,
)
from nexus_ai.core.agents import (
    AgentOrchestrator,
    Agent,
    AgentTask,
    AgentType,
    AgentStatus,
    TaskPriority,
    get_orchestrator,
    quick_task,
)
from nexus_ai.core.gpu_accelerator import (
    GPUAccelerator,
    GPUConfig,
    GPUInfo,
    get_gpu_accelerator,
    check_gpu_status,
)
from nexus_ai.core.backup_system import (
    BackupManager,
    BackupConfig,
    BackupRecord,
    BackupType,
    BackupStatus,
    RestorePoint,
    get_backup_manager,
    create_restore_point_before,
)
from nexus_ai.core.doc_automation import (
    DocumentationAgent,
    DocGenerator,
    CodeAnalyzer,
    DiagramGenerator,
    get_doc_agent,
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
