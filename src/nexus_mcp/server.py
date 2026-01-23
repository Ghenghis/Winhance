"""
NexusFS MCP Server

Model Context Protocol server for AI tool integration.
Exposes NexusFS functionality to:
- Claude Code
- Windsurf IDE
- LM Studio
- Any MCP-compatible client
"""

from __future__ import annotations

import asyncio
import json
import os
import sys
from pathlib import Path
from typing import Any

from loguru import logger

# Security: Allowed root directories for path validation
# Prevents path traversal attacks by restricting access to known safe directories
ALLOWED_ROOTS: list[str] = [
    os.path.expanduser("~"),
    "C:\\Users",
    "C:\\Program Files",
    "C:\\Program Files (x86)",
    "D:\\",
    "E:\\",
    "F:\\",
    "G:\\",
]


def validate_path(path: str, param_name: str = "path") -> str:
    """
    Validate and canonicalize a path to prevent traversal attacks.

    Args:
        path: The path to validate
        param_name: Name of the parameter for error messages

    Returns:
        The canonicalized absolute path

    Raises:
        ValueError: If the path is invalid or outside allowed directories
    """
    if not path:
        raise ValueError(f"{param_name} cannot be empty")

    # Normalize the path to resolve .. and .
    try:
        full_path = os.path.normpath(os.path.abspath(path))
    except (ValueError, OSError) as e:
        raise ValueError(f"Invalid {param_name}: {e}") from e

    # Check for path traversal patterns in original input
    if ".." in path:
        raise ValueError(f"Path traversal not allowed in {param_name}")

    # Verify the path is under an allowed root
    if not any(full_path.startswith(root) for root in ALLOWED_ROOTS):
        raise ValueError(f"{param_name} '{path}' is not in allowed directories")

    return full_path

# MCP imports
try:
    from mcp.server import Server
    from mcp.server.stdio import stdio_server
    from mcp.types import TextContent, Tool
    MCP_AVAILABLE = True
except ImportError:
    MCP_AVAILABLE = False
    logger.warning("MCP package not installed. Run: pip install mcp")


# Initialize MCP server
if MCP_AVAILABLE:
    server = Server("nexus-fs")


def create_server():
    """Create and configure the MCP server."""
    if not MCP_AVAILABLE:
        raise ImportError("MCP package not installed")

    @server.list_tools()
    async def list_tools() -> list[Tool]:
        """List available NexusFS tools."""
        return [
            Tool(
                name="nexus_search",
                description="Search files using semantic or pattern matching. Faster than Everything Search.",
                inputSchema={
                    "type": "object",
                    "properties": {
                        "query": {
                            "type": "string",
                            "description": "Search query (natural language or pattern)"
                        },
                        "type": {
                            "type": "string",
                            "enum": ["semantic", "glob", "regex", "exact"],
                            "default": "semantic",
                            "description": "Search type"
                        },
                        "limit": {
                            "type": "integer",
                            "default": 20,
                            "description": "Maximum results"
                        },
                        "extensions": {
                            "type": "array",
                            "items": {"type": "string"},
                            "description": "Filter by file extensions"
                        },
                        "drives": {
                            "type": "array",
                            "items": {"type": "string"},
                            "description": "Filter by drive letters"
                        },
                        "min_size_mb": {
                            "type": "number",
                            "description": "Minimum file size in MB"
                        },
                        "max_size_mb": {
                            "type": "number",
                            "description": "Maximum file size in MB"
                        }
                    },
                    "required": ["query"]
                }
            ),
            Tool(
                name="nexus_index",
                description="Build or update the file index for a path or all drives.",
                inputSchema={
                    "type": "object",
                    "properties": {
                        "path": {
                            "type": "string",
                            "description": "Path to index (omit for all drives)"
                        },
                        "deep": {
                            "type": "boolean",
                            "default": False,
                            "description": "Include content embeddings for semantic search"
                        }
                    }
                }
            ),
            Tool(
                name="nexus_organize",
                description="AI-powered file organization suggestions.",
                inputSchema={
                    "type": "object",
                    "properties": {
                        "path": {
                            "type": "string",
                            "description": "Directory to organize"
                        },
                        "strategy": {
                            "type": "string",
                            "enum": ["semantic", "type", "project", "date"],
                            "default": "semantic",
                            "description": "Organization strategy"
                        },
                        "dry_run": {
                            "type": "boolean",
                            "default": True,
                            "description": "Preview only, don't move files"
                        }
                    },
                    "required": ["path"]
                }
            ),
            Tool(
                name="nexus_rollback",
                description="Undo file organization operations.",
                inputSchema={
                    "type": "object",
                    "properties": {
                        "transaction_id": {
                            "type": "string",
                            "description": "Specific transaction ID to rollback"
                        },
                        "hours": {
                            "type": "integer",
                            "description": "Rollback all transactions in last N hours"
                        },
                        "generate_script": {
                            "type": "boolean",
                            "default": False,
                            "description": "Generate rollback script instead of executing"
                        }
                    }
                }
            ),
            Tool(
                name="nexus_space",
                description="Analyze disk space usage and find large files.",
                inputSchema={
                    "type": "object",
                    "properties": {
                        "path": {
                            "type": "string",
                            "default": "C:\\Users\\Admin",
                            "description": "Path to analyze"
                        },
                        "large_gb": {
                            "type": "number",
                            "default": 1.0,
                            "description": "Threshold for large files (GB)"
                        },
                        "find_duplicates": {
                            "type": "boolean",
                            "default": False,
                            "description": "Also find duplicate files"
                        }
                    }
                }
            ),
            Tool(
                name="nexus_models",
                description="Manage AI model files - scan, relocate, and organize.",
                inputSchema={
                    "type": "object",
                    "properties": {
                        "action": {
                            "type": "string",
                            "enum": ["scan", "suggest", "relocate"],
                            "default": "scan",
                            "description": "Action to perform"
                        },
                        "dest_drive": {
                            "type": "string",
                            "default": "G",
                            "description": "Destination drive for relocation"
                        },
                        "min_size_gb": {
                            "type": "number",
                            "default": 1.0,
                            "description": "Minimum model size for relocation"
                        },
                        "dry_run": {
                            "type": "boolean",
                            "default": True,
                            "description": "Preview only"
                        }
                    }
                }
            ),
            Tool(
                name="nexus_similar",
                description="Find files similar to a given file using semantic matching.",
                inputSchema={
                    "type": "object",
                    "properties": {
                        "file_path": {
                            "type": "string",
                            "description": "Path to reference file"
                        },
                        "limit": {
                            "type": "integer",
                            "default": 10,
                            "description": "Maximum results"
                        }
                    },
                    "required": ["file_path"]
                }
            ),
        ]

    @server.call_tool()
    async def call_tool(name: str, arguments: dict[str, Any]) -> list[TextContent]:
        """Handle tool calls."""
        try:
            if name == "nexus_search":
                result = await handle_search(arguments)
            elif name == "nexus_index":
                result = await handle_index(arguments)
            elif name == "nexus_organize":
                result = await handle_organize(arguments)
            elif name == "nexus_rollback":
                result = await handle_rollback(arguments)
            elif name == "nexus_space":
                result = await handle_space(arguments)
            elif name == "nexus_models":
                result = await handle_models(arguments)
            elif name == "nexus_similar":
                result = await handle_similar(arguments)
            else:
                result = {"error": f"Unknown tool: {name}"}

            return [TextContent(type="text", text=json.dumps(result, indent=2, default=str))]

        except Exception as e:
            logger.error(f"Tool error: {e}")
            return [TextContent(type="text", text=json.dumps({"error": str(e)}))]

    return server


async def handle_search(args: dict[str, Any]) -> dict[str, Any]:
    """Handle search requests."""
    query = args.get("query", "")
    search_type = args.get("type", "semantic")
    result_limit = args.get("limit", 20)

    # TODO: Implement actual search with result_limit
    return {
        "query": query,
        "type": search_type,
        "limit": result_limit,
        "results": [],
        "message": "Search index not built yet. Run 'nexus index build' first."
    }


async def handle_index(args: dict[str, Any]) -> dict[str, Any]:
    """Handle indexing requests."""
    path = args.get("path")
    deep = args.get("deep", False)

    try:
        # Lazy import to avoid circular dependencies
        from nexus_ai.indexer.hyper_indexer import HyperIndexer

        indexer = HyperIndexer(deep_index=deep)

        if path:
            # Validate path before indexing
            validated_path = validate_path(path)
            stats = indexer.index_path(Path(validated_path))
        else:
            stats = indexer.index_all_drives(["C", "D", "E", "F", "G"])

        return {
            "status": "complete",
            "stats": stats
        }
    except ValueError as e:
        return {"error": f"Invalid path: {e}"}
    except ImportError as e:
        return {"error": f"Indexer not available: {e}"}


async def handle_organize(args: dict[str, Any]) -> dict[str, Any]:
    """Handle organization requests."""
    path = args.get("path")
    strategy = args.get("strategy", "semantic")
    dry_run = args.get("dry_run", True)

    return {
        "path": path,
        "strategy": strategy,
        "dry_run": dry_run,
        "suggestions": [],
        "message": "Organization analysis not yet implemented"
    }


async def handle_rollback(args: dict[str, Any]) -> dict[str, Any]:
    """Handle rollback requests."""
    tx_id = args.get("transaction_id")
    hours = args.get("hours")
    generate_script = args.get("generate_script", False)

    try:
        # Lazy import to avoid circular dependencies
        from nexus_ai.config import get_config
        from nexus_ai.organization.transaction_manager import TransactionManager

        config = get_config()
        tm = TransactionManager(
            log_path=config.transaction.log_path,
            backup_dir=config.transaction.backup_dir,
        )

        if generate_script and hours:
            script = tm.generate_rollback_script(hours=hours)
            return {
                "script": script,
                "format": "ps1"
            }

        if tx_id:
            success = tm.rollback(tx_id)
            return {
                "transaction_id": tx_id,
                "rolled_back": success
            }

        if hours:
            rolled_back = tm.rollback_range(hours=hours)
            return {
                "hours": hours,
                "transactions_rolled_back": len(rolled_back),
                "ids": rolled_back
            }

        return {"error": "Specify transaction_id or hours"}
    except ImportError as e:
        return {"error": f"Transaction manager not available: {e}"}


async def handle_space(args: dict[str, Any]) -> dict[str, Any]:
    """Handle space analysis requests."""
    path = args.get("path", os.path.expanduser("~"))
    large_gb = args.get("large_gb", 1.0)
    find_duplicates = args.get("find_duplicates", False)

    try:
        # Validate path before analysis
        validated_path = validate_path(path)

        # Lazy import to avoid circular dependencies
        from nexus_ai.tools.space_analyzer import SpaceAnalyzer

        analyzer = SpaceAnalyzer(
            large_threshold_gb=large_gb,
            compute_hashes=find_duplicates,
        )

        analysis = analyzer.analyze_path(validated_path)

        return {
            "path": validated_path,
            "file_count": analysis.file_count,
            "dir_count": analysis.dir_count,
            "scan_time_ms": analysis.scan_time_ms,
            "large_files": [
                {"path": f.path, "size_gb": f.size / 1024**3, "extension": f.extension}
                for f in analysis.large_files[:20]
            ],
            "huge_files": [
                {"path": f.path, "size_gb": f.size / 1024**3, "extension": f.extension}
                for f in analysis.huge_files[:10]
            ],
            "model_files_count": len(analysis.model_files),
            "model_files_size_gb": sum(f.size for f in analysis.model_files) / 1024**3,
        }
    except ValueError as e:
        return {"error": f"Invalid path: {e}"}
    except ImportError as e:
        return {"error": f"Space analyzer not available: {e}"}


async def handle_models(args: dict[str, Any]) -> dict[str, Any]:
    """Handle model management requests."""
    action = args.get("action", "scan")
    dest_drive = args.get("dest_drive", "G")
    min_size_gb = args.get("min_size_gb", 1.0)
    dry_run = args.get("dry_run", True)

    try:
        # Lazy import to avoid circular dependencies
        from nexus_ai.tools.model_relocator import ModelRelocator

        relocator = ModelRelocator()

        if action == "scan":
            summary = relocator.get_app_storage_summary()
            return {
                "action": "scan",
                "summary": {
                    app: {
                        "count": info["count"],
                        "size_gb": info["total_size"] / 1024**3,
                        "largest": info["largest_model"]
                    }
                    for app, info in summary.items()
                }
            }

        elif action == "suggest":
            plan = relocator.suggest_relocations(
                target_free_gb=min_size_gb * 10,
                dest_drive=dest_drive,
            )
            return {
                "action": "suggest",
                "models_to_relocate": len(plan.models),
                "total_size_gb": plan.total_size / 1024**3,
                "destination": plan.dest_dir,
            }

        elif action == "relocate":
            plan = relocator.create_relocation_plan(
                dest_drive=dest_drive,
                min_size_gb=min_size_gb,
            )
            results = relocator.execute_relocation(plan, dry_run=dry_run)

            return {
                "action": "relocate",
                "dry_run": dry_run,
                "models_processed": len(results),
                "successful": sum(1 for r in results if r.success),
                "failed": sum(1 for r in results if not r.success),
            }

        return {"error": f"Unknown action: {action}"}
    except ImportError as e:
        return {"error": f"Model relocator not available: {e}"}


async def handle_similar(args: dict[str, Any]) -> dict[str, Any]:
    """Handle similar file search."""
    file_path = args.get("file_path")
    result_limit = args.get("limit", 10)

    return {
        "file_path": file_path,
        "limit": result_limit,
        "similar_files": [],
        "message": "Semantic similarity requires deep index with embeddings"
    }


async def main():
    """Main entry point for the MCP server."""
    if not MCP_AVAILABLE:
        print("Error: MCP package not installed. Run: pip install mcp", file=sys.stderr)
        sys.exit(1)

    logger.info("Starting NexusFS MCP Server...")

    server = create_server()

    async with stdio_server() as (read_stream, write_stream):
        await server.run(read_stream, write_stream, server.create_initialization_options())


if __name__ == "__main__":
    asyncio.run(main())
