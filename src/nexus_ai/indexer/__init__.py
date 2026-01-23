"""
NexusFS Hyper-Fast Indexer

Faster than Everything Search through:
- Aggressive multi-threading (CPU cores * 4 for I/O)
- Memory-mapped file scanning
- USN Journal for real-time updates
- Parallel hash computation
- SIMD-optimized operations
"""

from nexus_ai.indexer.hyper_indexer import HyperIndexer

__all__ = ["HyperIndexer"]
