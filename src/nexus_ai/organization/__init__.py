"""
File organization module.

Provides AI-powered file organization, transaction management,
and safe file operations with rollback capabilities.
"""

from nexus_ai.organization.transaction_manager import (
    FileTransaction,
    TransactionManager,
    TransactionStatus,
)

__all__ = [
    "TransactionManager",
    "FileTransaction",
    "TransactionStatus",
]
