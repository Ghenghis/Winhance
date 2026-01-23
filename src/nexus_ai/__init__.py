"""
NexusFS AI Layer - Ultra-Fast AI-Powered File Organization

This module provides:
- Hyper-threaded file indexing (faster than Everything Search)
- Semantic search using embeddings
- AI-powered file organization
- Space analysis and cleanup tools
- Model relocation utilities
- Enterprise logging and monitoring
- Multi-provider AI integration
- Autonomous agent orchestration
- GPU-accelerated operations
- Multi-location backup system
- Automated documentation
"""

__version__ = "0.1.0"
__author__ = "ShadowByte"

from nexus_ai.config import NexusConfig, get_config
from nexus_ai.organization.transaction_manager import TransactionManager

# Core enterprise modules
from nexus_ai.core import (
    # Logging
    setup_logging,
    get_logger,
    LogConfig,
    LogLevel,
    LogPerformance,
    # AI Providers
    AIProviderManager,
    AIProvider,
    AIConfig,
    AIMessage,
    AIResponse,
    ProviderType,
    get_ai_manager,
    # Agents
    AgentOrchestrator,
    Agent,
    AgentTask,
    AgentType,
    AgentStatus,
    TaskPriority,
    get_orchestrator,
    quick_task,
    # GPU
    GPUAccelerator,
    GPUConfig,
    check_gpu_status,
    get_gpu_accelerator,
    # Backup
    BackupManager,
    BackupConfig,
    get_backup_manager,
    create_restore_point_before,
    # Documentation
    DocumentationAgent,
    get_doc_agent,
)

__all__ = [
    # Config
    "NexusConfig",
    "get_config",
    "TransactionManager",
    # Logging
    "setup_logging",
    "get_logger",
    "LogConfig",
    "LogLevel",
    "LogPerformance",
    # AI
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
    "check_gpu_status",
    "get_gpu_accelerator",
    # Backup
    "BackupManager",
    "BackupConfig",
    "get_backup_manager",
    "create_restore_point_before",
    # Documentation
    "DocumentationAgent",
    "get_doc_agent",
]
