# AI Agent Automation

## Overview

Winhance-FS includes a Python-based AI agent system for automated file management, browser automation, and multi-agent workflows.

## Architecture

```
nexus-agents/
+-- pyproject.toml
+-- src/nexus_agents/
    +-- core/                    # Agent framework
    |   +-- __init__.py
    |   +-- base_agent.py        # Base agent class
    |   +-- agent_runtime.py     # Execution runtime
    |   +-- orchestrator.py      # Multi-agent coordination
    +-- agents/                  # Individual agents
    |   +-- __init__.py
    |   +-- file_discovery.py    # File scanning agent
    |   +-- classification.py    # AI classification agent
    |   +-- organization.py      # File organization agent
    |   +-- cleanup.py           # Safe deletion agent
    |   +-- report.py            # Report generation agent
    +-- browser/                 # Browser automation
    |   +-- __init__.py
    |   +-- playwright_mcp.py    # Playwright MCP server
    |   +-- browser_use.py       # browser-use integration
    +-- mcp/                     # MCP server
    |   +-- __init__.py
    |   +-- server.py            # FastMCP server
    |   +-- tools/               # MCP tools
    +-- llm/                     # LLM integrations
        +-- __init__.py
        +-- openai.py
        +-- anthropic.py
        +-- local.py             # LM Studio, Ollama
```

## Installation

```bash
cd src/nexus-agents

# Create virtual environment
python -m venv .venv
.venv\Scripts\activate  # Windows
# source .venv/bin/activate  # Linux/Mac

# Install package
pip install -e .[dev]

# Install Playwright browsers
playwright install chromium
```

## Agent Framework

### Base Agent

All agents inherit from `BaseAgent`:

```python
# core/base_agent.py
from abc import ABC, abstractmethod
from dataclasses import dataclass
from typing import Any

@dataclass
class AgentResult:
    success: bool
    data: dict[str, Any] | None = None
    error: str | None = None

class BaseAgent(ABC):
    """Base class for all agents."""

    name: str
    description: str

    @abstractmethod
    async def execute(
        self,
        task: str,
        context: dict | None = None
    ) -> AgentResult:
        """Execute the agent's task."""
        pass

    async def __call__(self, task: str, **kwargs) -> AgentResult:
        """Allow agents to be called directly."""
        return await self.execute(task, context=kwargs)
```

### Agent Runtime

```python
# core/agent_runtime.py
from typing import Type
from .base_agent import BaseAgent, AgentResult

class AgentRuntime:
    """Manages agent execution and lifecycle."""

    def __init__(self):
        self._agents: dict[str, BaseAgent] = {}

    def register(self, agent_class: Type[BaseAgent]) -> None:
        """Register an agent."""
        agent = agent_class()
        self._agents[agent.name] = agent

    async def run(
        self,
        agent_name: str,
        task: str,
        context: dict | None = None
    ) -> AgentResult:
        """Run a specific agent."""
        agent = self._agents.get(agent_name)
        if not agent:
            return AgentResult(
                success=False,
                error=f"Agent not found: {agent_name}"
            )

        return await agent.execute(task, context)
```

## Available Agents

### FileDiscoveryAgent

Scans directories for files matching criteria:

```python
# agents/file_discovery.py
class FileDiscoveryAgent(BaseAgent):
    name = "file_discovery"
    description = "Scans directories for files matching criteria"

    async def execute(
        self,
        task: str,
        context: dict | None = None
    ) -> AgentResult:
        path = context.get("path", ".")
        pattern = context.get("pattern", "*")
        min_size = context.get("min_size_mb", 0)

        files = []
        for file_path in Path(path).rglob(pattern):
            if file_path.is_file():
                size_mb = file_path.stat().st_size / (1024 * 1024)
                if size_mb >= min_size:
                    files.append({
                        "path": str(file_path),
                        "size_mb": round(size_mb, 2),
                        "modified": file_path.stat().st_mtime
                    })

        return AgentResult(
            success=True,
            data={"files": files, "count": len(files)}
        )
```

### ClassificationAgent

AI-powered file classification:

```python
# agents/classification.py
class ClassificationAgent(BaseAgent):
    name = "classification"
    description = "Classifies files using AI"

    def __init__(self):
        self._embeddings = None  # Lazy load ONNX model

    async def execute(
        self,
        task: str,
        context: dict | None = None
    ) -> AgentResult:
        files = context.get("files", [])

        classifications = []
        for file_info in files:
            file_type = await self._classify_file(file_info["path"])
            classifications.append({
                **file_info,
                "classification": file_type
            })

        return AgentResult(
            success=True,
            data={"classified": classifications}
        )

    async def _classify_file(self, path: str) -> str:
        # Use entropy, magic bytes, and AI for classification
        entropy = calculate_entropy(path)
        magic = detect_magic_bytes(path)

        if entropy > 7.5:
            return "encrypted"
        elif magic in AI_MODEL_SIGNATURES:
            return "ai_model"
        # ... more classification logic

        return "unknown"
```

### OrganizationAgent

Suggests and executes file organization:

```python
# agents/organization.py
class OrganizationAgent(BaseAgent):
    name = "organization"
    description = "Organizes files based on patterns"

    async def execute(
        self,
        task: str,
        context: dict | None = None
    ) -> AgentResult:
        files = context.get("files", [])
        strategy = context.get("strategy", "type")
        auto_execute = context.get("auto_execute", False)

        suggestions = self._generate_suggestions(files, strategy)

        if auto_execute:
            results = await self._execute_moves(suggestions)
            return AgentResult(
                success=True,
                data={
                    "suggestions": suggestions,
                    "executed": results
                }
            )

        return AgentResult(
            success=True,
            data={"suggestions": suggestions}
        )
```

### CleanupAgent

Safe file deletion with rollback:

```python
# agents/cleanup.py
class CleanupAgent(BaseAgent):
    name = "cleanup"
    description = "Safely deletes files with rollback support"

    async def execute(
        self,
        task: str,
        context: dict | None = None
    ) -> AgentResult:
        files = context.get("files", [])
        to_recycle = context.get("to_recycle", True)

        transaction_id = generate_transaction_id()

        for file_info in files:
            await self._safe_delete(
                file_info["path"],
                transaction_id,
                to_recycle
            )

        return AgentResult(
            success=True,
            data={
                "transaction_id": transaction_id,
                "deleted_count": len(files)
            }
        )
```

## Multi-Agent Orchestration

### Orchestration Patterns

```python
# core/orchestrator.py
from enum import Enum

class OrchestrationPattern(Enum):
    SEQUENTIAL = "sequential"    # A -> B -> C
    CONCURRENT = "concurrent"    # A, B, C in parallel
    GROUP_CHAT = "group_chat"    # Agents collaborate
    HANDOFF = "handoff"          # Pass context between agents
    MAGENTIC = "magentic"        # Manager coordinates
```

### Example: Cleanup Workflow

```python
from nexus_agents.core.orchestrator import Orchestrator

async def cleanup_workflow():
    orchestrator = Orchestrator()

    # Define workflow
    workflow = orchestrator.create_workflow([
        {
            "agent": "file_discovery",
            "task": "Find large unused files",
            "context": {
                "path": "C:\\Users\\Admin",
                "min_size_mb": 100,
                "pattern": "*"
            }
        },
        {
            "agent": "classification",
            "task": "Classify discovered files",
            # Uses output from previous step
        },
        {
            "agent": "organization",
            "task": "Suggest organization",
            "context": {"strategy": "semantic"}
        },
        {
            "agent": "cleanup",
            "task": "Clean safe-to-delete files",
            "context": {"to_recycle": True}
        }
    ])

    # Execute sequentially
    result = await orchestrator.run(
        workflow,
        pattern=OrchestrationPattern.SEQUENTIAL
    )

    return result
```

### Example: Parallel Scan

```python
async def parallel_scan():
    orchestrator = Orchestrator()

    # Scan multiple drives in parallel
    tasks = [
        {
            "agent": "file_discovery",
            "task": f"Scan {drive}",
            "context": {"path": f"{drive}:\\"}
        }
        for drive in ["C", "D", "E"]
    ]

    result = await orchestrator.run(
        tasks,
        pattern=OrchestrationPattern.CONCURRENT
    )

    # Aggregate results
    all_files = []
    for task_result in result.data["results"]:
        all_files.extend(task_result.data["files"])

    return all_files
```

## Browser Automation

### Playwright MCP Server

```python
# browser/playwright_mcp.py
from mcp.server.fastmcp import FastMCP
from playwright.async_api import async_playwright

mcp = FastMCP("Playwright Browser Server")

class BrowserState:
    def __init__(self):
        self.browser = None
        self.page = None

browser_state = BrowserState()

@mcp.tool()
async def playwright_navigate(url: str) -> str:
    """Navigate to a URL."""
    if not browser_state.page:
        pw = await async_playwright().start()
        browser_state.browser = await pw.chromium.launch()
        browser_state.page = await browser_state.browser.new_page()

    await browser_state.page.goto(url)
    title = await browser_state.page.title()
    return f"Navigated to {url}. Title: {title}"

@mcp.tool()
async def playwright_click(selector: str) -> str:
    """Click an element."""
    await browser_state.page.click(selector)
    return f"Clicked: {selector}"

@mcp.tool()
async def playwright_type(selector: str, text: str) -> str:
    """Type text into an input."""
    await browser_state.page.fill(selector, text)
    return f"Typed '{text}' into {selector}"

@mcp.tool()
async def playwright_screenshot(filename: str = "screenshot.png") -> str:
    """Take a screenshot."""
    await browser_state.page.screenshot(path=filename)
    return f"Screenshot saved: {filename}"

@mcp.tool()
async def playwright_get_text() -> str:
    """Get page text content."""
    text = await browser_state.page.evaluate(
        "() => document.body.innerText"
    )
    return text[:5000]  # Limit for context
```

### browser-use Integration

```python
# browser/browser_use.py
from browser_use import Agent, Browser

async def autonomous_web_task(task: str, llm_config: dict):
    """Execute autonomous web task using browser-use."""
    agent = Agent(
        task=task,
        llm=get_llm(llm_config),
        browser=Browser(headless=True),
    )

    result = await agent.run()
    return result

# Example usage
async def download_model(model_id: str):
    return await autonomous_web_task(
        task=f"Navigate to HuggingFace and download {model_id}",
        llm_config={"model": "gpt-4o"}
    )
```

## LLM Integrations

### OpenAI

```python
# llm/openai.py
from openai import AsyncOpenAI

class OpenAIProvider:
    def __init__(self, api_key: str):
        self.client = AsyncOpenAI(api_key=api_key)

    async def complete(self, prompt: str, **kwargs) -> str:
        response = await self.client.chat.completions.create(
            model=kwargs.get("model", "gpt-4o"),
            messages=[{"role": "user", "content": prompt}]
        )
        return response.choices[0].message.content
```

### Anthropic

```python
# llm/anthropic.py
from anthropic import AsyncAnthropic

class AnthropicProvider:
    def __init__(self, api_key: str):
        self.client = AsyncAnthropic(api_key=api_key)

    async def complete(self, prompt: str, **kwargs) -> str:
        response = await self.client.messages.create(
            model=kwargs.get("model", "claude-3-5-sonnet-20241022"),
            max_tokens=4096,
            messages=[{"role": "user", "content": prompt}]
        )
        return response.content[0].text
```

### Local (LM Studio / Ollama)

```python
# llm/local.py
from openai import AsyncOpenAI

class LocalProvider:
    """Uses OpenAI-compatible API for local LLMs."""

    def __init__(self, base_url: str = "http://localhost:1234/v1"):
        self.client = AsyncOpenAI(
            base_url=base_url,
            api_key="lm-studio"  # Not used but required
        )

    async def complete(self, prompt: str, **kwargs) -> str:
        response = await self.client.chat.completions.create(
            model=kwargs.get("model", "local-model"),
            messages=[{"role": "user", "content": prompt}]
        )
        return response.choices[0].message.content
```

## Testing

```python
# tests/test_agents.py
import pytest
from nexus_agents.agents.file_discovery import FileDiscoveryAgent

@pytest.mark.asyncio
async def test_file_discovery():
    agent = FileDiscoveryAgent()
    result = await agent.execute(
        task="Find Python files",
        context={
            "path": ".",
            "pattern": "*.py"
        }
    )

    assert result.success
    assert "files" in result.data
    assert len(result.data["files"]) > 0
```

Run tests:
```bash
pytest tests/ -v
```

## Configuration

Environment variables:

```bash
# LLM API keys
OPENAI_API_KEY=sk-...
ANTHROPIC_API_KEY=sk-ant-...

# Local LLM
LM_STUDIO_URL=http://localhost:1234/v1

# Agent settings
NEXUS_DATA_DIR=D:\Winhance-FS\data
NEXUS_LOG_LEVEL=INFO
```

## CLI Usage

```bash
# Run MCP server
python -m nexus_agents.mcp.server

# Run specific agent
python -m nexus_agents.cli run file_discovery --path "C:\Users" --pattern "*.gguf"

# Run workflow
python -m nexus_agents.cli workflow cleanup --auto-execute false
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for Python-specific guidelines:
- Use Black for formatting
- Add type hints to all functions
- Write tests for new agents
- Document with docstrings
