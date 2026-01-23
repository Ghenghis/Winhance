"""
NexusFS MCP Server Entry Point

Run with: python -m nexus_mcp
"""

import asyncio

from nexus_mcp.server import main

if __name__ == "__main__":
    asyncio.run(main())
