# MCP Integration Guide

## Overview

Winhance-FS provides an MCP (Model Context Protocol) server that enables AI tools like Claude Code, Windsurf IDE, and LM Studio to access file system operations.

## What is MCP?

MCP (Model Context Protocol) is a standard for AI tool integration that allows LLMs to:
- Execute tools and functions
- Access resources and data
- Perform actions on behalf of users

## Supported Clients

| Client | Status | Configuration |
|--------|--------|---------------|
| Claude Code | Supported | `~/.claude/claude_desktop_config.json` |
| Windsurf IDE | Supported | Settings > MCP Servers |
| LM Studio | Supported | OpenAI-compatible API |
| Cursor IDE | Supported | Settings > MCP |
| VS Code | Supported | With MCP extension |

## Available Tools

### File Operations

| Tool | Description | Parameters |
|------|-------------|------------|
| `nexus_scan` | Scan drives for space analysis | `drive_letter?` |
| `nexus_search` | Search files by pattern | `query`, `options?` |
| `nexus_move` | Move file with transaction | `source`, `dest`, `symlink?` |
| `nexus_delete` | Safe delete with backup | `path`, `to_recycle?` |
| `nexus_rollback` | Undo operation | `transaction_id` |

### Storage Intelligence

| Tool | Description | Parameters |
|------|-------------|------------|
| `nexus_recovery` | Get space recovery items | `drive_letter?` |
| `nexus_models` | List AI model files | `path?` |
| `nexus_duplicates` | Find duplicate files | `path?`, `by_hash?` |
| `nexus_large_files` | Find large files | `threshold_mb?` |

### Forensics

| Tool | Description | Parameters |
|------|-------------|------------|
| `nexus_ads` | Get alternate data streams | `path` |
| `nexus_vss` | List shadow copies | `drive_letter` |
| `nexus_entropy` | Calculate file entropy | `path` |
| `nexus_classify` | Classify file type | `path` |

### Browser Automation

| Tool | Description | Parameters |
|------|-------------|------------|
| `nexus_navigate` | Navigate to URL | `url` |
| `nexus_click` | Click element | `selector` |
| `nexus_type` | Type text | `selector`, `text` |
| `nexus_screenshot` | Capture page | `filename?` |

## Configuration

### Claude Code / Claude Desktop

Add to `~/.claude/claude_desktop_config.json` (Windows: `%APPDATA%\Claude\claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "winhance-fs": {
      "command": "python",
      "args": ["-m", "nexus_agents.mcp.server"],
      "env": {
        "NEXUS_DATA_DIR": "D:\\Winhance-FS\\data"
      }
    }
  }
}
```

### Windsurf IDE

1. Open Settings > MCP Servers
2. Add new server:
   - Name: `winhance-fs`
   - Command: `python -m nexus_agents.mcp.server`
   - Working Directory: `D:\Winhance-FS`

### LM Studio

LM Studio uses OpenAI-compatible API. Configure the endpoint:

```json
{
  "base_url": "http://localhost:1234/v1",
  "api_key": "lm-studio",
  "model": "local-model"
}
```

### Cursor IDE

Add to `.cursor/mcp.json`:

```json
{
  "servers": {
    "winhance-fs": {
      "command": "python",
      "args": ["-m", "nexus_agents.mcp.server"]
    }
  }
}
```

## Usage Examples

### Claude Code

```
User: Scan my C: drive and show me what's taking up space

Claude: I'll scan your C: drive using the Storage Intelligence module.

[Calls nexus_scan with drive_letter="C:"]

Here's what I found:
- .lmstudio: 337 GB (AI models)
- .ollama: 163 GB (AI models)
- .cache: 44 GB (various caches)
- Temp files: 12 GB

Would you like me to relocate the AI models to another drive?
```

### Windsurf IDE

```
User: Find all duplicate files in my Documents folder

Windsurf: [Calls nexus_duplicates with path="C:\Users\Admin\Documents"]

Found 47 duplicate files totaling 2.3 GB:
- report.pdf (5 copies) - 15 MB
- photo_backup.zip (3 copies) - 1.2 GB
...

Would you like me to remove the duplicates?
```

### Automated Workflow

```python
# Example: Automated cleanup workflow
async def cleanup_workflow():
    # 1. Scan for space recovery opportunities
    scan_result = await nexus_scan()

    # 2. Get AI model locations
    models = await nexus_models()

    # 3. Relocate large models
    for model in models:
        if model.size_gb > 10:
            await nexus_move(
                source=model.path,
                dest=f"D:\\Models\\{model.name}",
                symlink=True
            )

    # 4. Clean temp files
    await nexus_delete(
        path="C:\\Users\\Admin\\AppData\\Local\\Temp",
        to_recycle=True
    )

    # 5. Generate report
    report = await nexus_recovery()
    return report
```

## MCP Server Implementation

### Python Server (FastMCP)

```python
# nexus_agents/mcp/server.py
from mcp.server.fastmcp import FastMCP

mcp = FastMCP("Winhance-FS Storage Intelligence")

@mcp.tool()
async def nexus_scan(drive_letter: str | None = None) -> dict:
    """
    Scan drives for space analysis and recovery opportunities.

    Args:
        drive_letter: Optional drive to scan (e.g., "C:"). Scans all if not specified.

    Returns:
        Drive information and space recovery recommendations.
    """
    from nexus_agents.core.storage import StorageScanner
    scanner = StorageScanner()
    return await scanner.scan(drive_letter)

@mcp.tool()
async def nexus_search(
    query: str,
    drive_letter: str | None = None,
    max_results: int = 100
) -> list[dict]:
    """
    Search for files matching a pattern.

    Args:
        query: Search pattern (supports wildcards: *.gguf, *report*)
        drive_letter: Optional drive to search
        max_results: Maximum results to return

    Returns:
        List of matching files with path, size, and modification time.
    """
    from nexus_agents.core.search import FileSearcher
    searcher = FileSearcher()
    return await searcher.search(query, drive_letter, max_results)

@mcp.tool()
async def nexus_move(
    source: str,
    destination: str,
    create_symlink: bool = True,
    verify: bool = True
) -> dict:
    """
    Move a file or folder with transaction logging and optional symlink.

    Args:
        source: Source path
        destination: Destination path
        create_symlink: Create symlink at original location
        verify: Verify file integrity after move

    Returns:
        Transaction ID and operation status.
    """
    from nexus_agents.core.operations import FileOperations
    ops = FileOperations()
    return await ops.move(source, destination, create_symlink, verify)

@mcp.tool()
async def nexus_rollback(transaction_id: str) -> dict:
    """
    Rollback a previous file operation.

    Args:
        transaction_id: ID of the transaction to undo

    Returns:
        Rollback status and restored files.
    """
    from nexus_agents.core.operations import FileOperations
    ops = FileOperations()
    return await ops.rollback(transaction_id)

# Browser automation tools
@mcp.tool()
async def nexus_navigate(url: str) -> str:
    """Navigate browser to URL."""
    from nexus_agents.browser.playwright_mcp import browser
    return await browser.navigate(url)

@mcp.tool()
async def nexus_click(selector: str) -> str:
    """Click element by CSS selector."""
    from nexus_agents.browser.playwright_mcp import browser
    return await browser.click(selector)

if __name__ == "__main__":
    mcp.run(transport="stdio")
```

## Security Considerations

### Permissions

MCP tools operate with the same permissions as the MCP server process. Sensitive operations require:

- **Admin elevation**: Memory recovery, driver operations
- **User confirmation**: Permanent deletions, large moves
- **Rollback support**: All file modifications

### Sandboxing

By default, operations are preview-only. Enable execution with:

```json
{
  "mcpServers": {
    "winhance-fs": {
      "command": "python",
      "args": ["-m", "nexus_agents.mcp.server"],
      "env": {
        "NEXUS_EXECUTE_MODE": "true"
      }
    }
  }
}
```

### Logging

All MCP operations are logged to:
- `%APPDATA%\Winhance-FS\logs\mcp.log`
- Transaction database for rollback

## Troubleshooting

### Server Not Starting

```bash
# Check Python installation
python --version  # Requires 3.11+

# Install dependencies
pip install nexus-agents

# Test server manually
python -m nexus_agents.mcp.server
```

### Connection Issues

1. Verify server is running
2. Check configuration path
3. Restart AI client
4. Check firewall settings

### Tool Not Found

Ensure `nexus-agents` package is installed:

```bash
pip install -e src/nexus-agents
```

## Development

### Adding New Tools

```python
@mcp.tool()
async def my_new_tool(param1: str, param2: int = 10) -> dict:
    """
    Description of what the tool does.

    Args:
        param1: Description of param1
        param2: Description of param2 (default: 10)

    Returns:
        Description of return value.
    """
    # Implementation
    return {"result": "success"}
```

### Testing Tools

```bash
# Run tests
pytest src/nexus-agents/tests/test_mcp.py -v

# Manual testing
python -c "
import asyncio
from nexus_agents.mcp.server import nexus_scan
result = asyncio.run(nexus_scan('C:'))
print(result)
"
```
