"""
Agent Orchestration System

Autonomous agents for file management automation:
- Organization Agent: Sorts and organizes files
- Cleanup Agent: Identifies and removes junk
- Research Agent: Web search for file context
- Repair Agent: Fixes broken links, corrupted files
- Monitoring Agent: Real-time file system watching

Supports parallel agent execution and task queuing.
"""

from __future__ import annotations

import asyncio
import uuid
from abc import ABC, abstractmethod
from dataclasses import dataclass, field
from datetime import datetime
from enum import Enum
from pathlib import Path
from typing import Optional, Dict, Any, List, Callable, Awaitable
import threading
import queue

from nexus_ai.core.logging_config import get_logger, LogPerformance
from nexus_ai.core.ai_providers import (
    AIProviderManager,
    AIMessage,
    AIResponse,
    ProviderType,
    get_ai_manager,
)

logger = get_logger("agents")


class AgentStatus(Enum):
    """Agent execution status."""
    IDLE = "idle"
    RUNNING = "running"
    PAUSED = "paused"
    COMPLETED = "completed"
    FAILED = "failed"
    CANCELLED = "cancelled"


class TaskPriority(Enum):
    """Task priority levels."""
    LOW = 1
    NORMAL = 2
    HIGH = 3
    URGENT = 4
    CRITICAL = 5


class AgentType(Enum):
    """Types of agents available."""
    ORGANIZER = "organizer"
    CLEANUP = "cleanup"
    RESEARCH = "research"
    REPAIR = "repair"
    MONITOR = "monitor"
    SEARCH = "search"
    BACKUP = "backup"
    ANALYTICS = "analytics"


@dataclass
class AgentTask:
    """A task to be executed by an agent."""
    id: str = field(default_factory=lambda: str(uuid.uuid4())[:8])
    type: AgentType = AgentType.ORGANIZER
    description: str = ""
    parameters: Dict[str, Any] = field(default_factory=dict)
    priority: TaskPriority = TaskPriority.NORMAL
    created_at: datetime = field(default_factory=datetime.now)
    started_at: Optional[datetime] = None
    completed_at: Optional[datetime] = None
    status: AgentStatus = AgentStatus.IDLE
    result: Optional[Dict[str, Any]] = None
    error: Optional[str] = None
    parent_task_id: Optional[str] = None
    subtasks: List[str] = field(default_factory=list)


@dataclass
class AgentConfig:
    """Configuration for an agent."""
    type: AgentType
    name: str
    description: str
    enabled: bool = True
    auto_run: bool = False
    run_interval_seconds: int = 300
    max_concurrent_tasks: int = 5
    ai_provider: Optional[ProviderType] = None
    ai_model: Optional[str] = None
    parameters: Dict[str, Any] = field(default_factory=dict)


class Agent(ABC):
    """Abstract base class for all agents."""

    def __init__(self, config: AgentConfig, ai_manager: Optional[AIProviderManager] = None):
        self.config = config
        self.ai_manager = ai_manager or get_ai_manager()
        self._status = AgentStatus.IDLE
        self._current_task: Optional[AgentTask] = None
        self._task_history: List[AgentTask] = []
        self._lock = threading.Lock()

    @property
    def status(self) -> AgentStatus:
        return self._status

    @abstractmethod
    async def execute(self, task: AgentTask) -> Dict[str, Any]:
        """Execute a task. Must be implemented by subclasses."""
        pass

    async def run_task(self, task: AgentTask) -> AgentTask:
        """Run a task with full lifecycle management."""
        with self._lock:
            self._current_task = task
            self._status = AgentStatus.RUNNING
            task.status = AgentStatus.RUNNING
            task.started_at = datetime.now()

        logger.info(f"Agent {self.config.name} starting task {task.id}", task_type=task.type.value)

        try:
            with LogPerformance(f"Agent task {task.id}"):
                result = await self.execute(task)
                task.result = result
                task.status = AgentStatus.COMPLETED

        except asyncio.CancelledError:
            task.status = AgentStatus.CANCELLED
            task.error = "Task was cancelled"
            logger.warning(f"Task {task.id} cancelled")

        except Exception as e:
            task.status = AgentStatus.FAILED
            task.error = str(e)
            logger.exception(f"Task {task.id} failed: {e}")

        finally:
            task.completed_at = datetime.now()
            with self._lock:
                self._status = AgentStatus.IDLE
                self._current_task = None
                self._task_history.append(task)

        return task

    async def ai_chat(self, prompt: str, system_prompt: Optional[str] = None) -> str:
        """Send a prompt to the configured AI provider."""
        messages = []
        if system_prompt:
            messages.append(AIMessage(role="system", content=system_prompt))
        messages.append(AIMessage(role="user", content=prompt))

        response = await self.ai_manager.chat(
            messages,
            provider_type=self.config.ai_provider,
            model=self.config.ai_model,
        )
        return response.content


class OrganizerAgent(Agent):
    """Agent for organizing files based on AI analysis."""

    SYSTEM_PROMPT = """You are a file organization expert. Analyze files and suggest optimal organization.

Rules:
1. Group similar files together
2. Use clear, descriptive folder names
3. Consider file types, dates, and content
4. Preserve important metadata
5. Never delete files, only suggest moves

Output format: JSON with structure:
{
    "suggestions": [
        {"source": "path", "destination": "path", "reason": "explanation"}
    ],
    "summary": "brief summary"
}"""

    async def execute(self, task: AgentTask) -> Dict[str, Any]:
        path = task.parameters.get("path", ".")
        strategy = task.parameters.get("strategy", "semantic")
        dry_run = task.parameters.get("dry_run", True)

        # Analyze files
        from nexus_ai.tools.space_analyzer import SpaceAnalyzer
        analyzer = SpaceAnalyzer()

        files = []
        if Path(path).is_dir():
            for f in Path(path).iterdir():
                if f.is_file():
                    files.append({
                        "name": f.name,
                        "extension": f.suffix,
                        "size": f.stat().st_size,
                        "modified": datetime.fromtimestamp(f.stat().st_mtime).isoformat(),
                    })

        if not files:
            return {"suggestions": [], "message": "No files to organize"}

        # Ask AI for organization suggestions
        prompt = f"""Analyze these files and suggest organization:

Path: {path}
Strategy: {strategy}
Files: {files[:50]}  # Limit for context

Provide JSON organization suggestions."""

        response = await self.ai_chat(prompt, self.SYSTEM_PROMPT)

        # Parse response
        try:
            import json
            suggestions = json.loads(response)
        except json.JSONDecodeError:
            suggestions = {"suggestions": [], "raw_response": response}

        return {
            "path": path,
            "strategy": strategy,
            "dry_run": dry_run,
            "file_count": len(files),
            "suggestions": suggestions,
        }


class CleanupAgent(Agent):
    """Agent for identifying and cleaning up junk files."""

    SYSTEM_PROMPT = """You are a disk cleanup expert. Identify safe-to-delete files.

Categories to check:
1. Temporary files (.tmp, .temp, ~*)
2. Cache files
3. Old log files
4. Duplicate files
5. Empty directories
6. Orphaned files

NEVER suggest deleting:
- System files
- User documents without explicit permission
- Files accessed recently (< 7 days)
- Files with unique content

Output format: JSON with structure:
{
    "deletable": [{"path": "path", "size": bytes, "reason": "why safe"}],
    "total_size": bytes,
    "warnings": ["any concerns"]
}"""

    async def execute(self, task: AgentTask) -> Dict[str, Any]:
        path = task.parameters.get("path", "C:\\")
        min_age_days = task.parameters.get("min_age_days", 30)
        categories = task.parameters.get("categories", ["temp", "cache", "logs"])

        from nexus_ai.tools.space_analyzer import SpaceAnalyzer
        analyzer = SpaceAnalyzer()

        # Find candidates
        analysis = analyzer.analyze_path(path)

        candidates = []
        for f in analysis.temp_files[:100]:
            candidates.append({
                "path": f.path,
                "size": f.size,
                "category": "temp",
                "modified": f.modified.isoformat(),
            })

        for f in analysis.cache_files[:100]:
            candidates.append({
                "path": f.path,
                "size": f.size,
                "category": "cache",
                "modified": f.modified.isoformat(),
            })

        if not candidates:
            return {"deletable": [], "total_size": 0, "message": "No cleanup candidates found"}

        prompt = f"""Analyze these files for safe deletion:

Minimum age: {min_age_days} days
Categories: {categories}
Candidates: {candidates[:50]}

Identify which are safe to delete."""

        response = await self.ai_chat(prompt, self.SYSTEM_PROMPT)

        try:
            import json
            result = json.loads(response)
        except json.JSONDecodeError:
            result = {"deletable": [], "raw_response": response}

        return result


class ResearchAgent(Agent):
    """Agent for researching files and gathering context."""

    SYSTEM_PROMPT = """You are a research assistant specialized in file analysis.

Your tasks:
1. Identify file types and their purposes
2. Find documentation for unknown formats
3. Suggest applications to open files
4. Identify potentially important files
5. Detect patterns and relationships

Be thorough but concise. Cite sources when possible."""

    async def execute(self, task: AgentTask) -> Dict[str, Any]:
        query = task.parameters.get("query", "")
        file_path = task.parameters.get("file_path")
        context = task.parameters.get("context", {})

        prompt = f"""Research request: {query}

File: {file_path if file_path else 'N/A'}
Context: {context}

Provide detailed analysis and recommendations."""

        response = await self.ai_chat(prompt, self.SYSTEM_PROMPT)

        return {
            "query": query,
            "file_path": file_path,
            "analysis": response,
        }


class RepairAgent(Agent):
    """Agent for repairing broken links and corrupted files."""

    SYSTEM_PROMPT = """You are a file repair specialist.

Your capabilities:
1. Detect broken shortcuts and symlinks
2. Find moved/renamed files
3. Suggest fixes for corrupted files
4. Repair file associations
5. Fix permission issues

Always preserve original files before any repair attempt."""

    async def execute(self, task: AgentTask) -> Dict[str, Any]:
        path = task.parameters.get("path", ".")
        repair_type = task.parameters.get("repair_type", "all")

        issues = []

        # Check for broken shortcuts
        if Path(path).is_dir():
            for f in Path(path).rglob("*.lnk"):
                # Would check if shortcut target exists
                issues.append({
                    "type": "shortcut",
                    "path": str(f),
                    "status": "check_needed",
                })

        return {
            "path": path,
            "repair_type": repair_type,
            "issues_found": len(issues),
            "issues": issues[:50],
        }


class MonitorAgent(Agent):
    """Agent for real-time file system monitoring."""

    SYSTEM_PROMPT = """You are a file system monitor.

Watch for:
1. Large file downloads
2. Unusual file activity
3. Disk space changes
4. New files in monitored folders
5. Deleted important files

Alert immediately for critical issues."""

    def __init__(self, config: AgentConfig, ai_manager: Optional[AIProviderManager] = None):
        super().__init__(config, ai_manager)
        self._watchers: Dict[str, Any] = {}
        self._events: queue.Queue = queue.Queue()

    async def execute(self, task: AgentTask) -> Dict[str, Any]:
        action = task.parameters.get("action", "status")
        paths = task.parameters.get("paths", [])

        if action == "start":
            for path in paths:
                # Would start file system watcher
                self._watchers[path] = {"status": "watching", "since": datetime.now().isoformat()}
            return {"action": "started", "watching": list(self._watchers.keys())}

        elif action == "stop":
            for path in paths:
                if path in self._watchers:
                    del self._watchers[path]
            return {"action": "stopped", "watching": list(self._watchers.keys())}

        else:  # status
            return {
                "watchers": self._watchers,
                "event_count": self._events.qsize(),
            }


class SearchAgent(Agent):
    """Agent for intelligent file searching."""

    SYSTEM_PROMPT = """You are a search expert. Help users find files.

Capabilities:
1. Natural language to search query conversion
2. Fuzzy matching
3. Content-based search
4. Pattern recognition
5. Historical search analysis

Optimize queries for speed and relevance."""

    async def execute(self, task: AgentTask) -> Dict[str, Any]:
        query = task.parameters.get("query", "")
        search_type = task.parameters.get("type", "semantic")
        scope = task.parameters.get("scope", ["C:\\"])

        # Parse natural language query
        prompt = f"""Convert this natural language query to structured search:

Query: "{query}"
Search type: {search_type}
Scope: {scope}

Output JSON with:
- keywords: list of search terms
- extensions: file extensions to filter
- date_range: if mentioned
- size_range: if mentioned
- paths: specific paths mentioned"""

        response = await self.ai_chat(prompt, self.SYSTEM_PROMPT)

        try:
            import json
            parsed = json.loads(response)
        except json.JSONDecodeError:
            parsed = {"keywords": [query]}

        return {
            "query": query,
            "parsed": parsed,
            "results": [],  # Would be populated by actual search
        }


class AgentOrchestrator:
    """
    Orchestrates multiple agents for automated file management.

    Features:
    - Parallel agent execution
    - Task queue with priorities
    - Agent lifecycle management
    - Inter-agent communication
    """

    def __init__(self, ai_manager: Optional[AIProviderManager] = None):
        self.ai_manager = ai_manager or get_ai_manager()
        self._agents: Dict[AgentType, Agent] = {}
        self._task_queue: asyncio.PriorityQueue = asyncio.PriorityQueue()
        self._running = False
        self._workers: List[asyncio.Task] = []
        self._max_workers = 5

    def register_agent(self, agent_type: AgentType, config: Optional[AgentConfig] = None) -> None:
        """Register an agent with the orchestrator."""
        if config is None:
            config = AgentConfig(
                type=agent_type,
                name=f"{agent_type.value}_agent",
                description=f"Default {agent_type.value} agent",
            )

        agent_classes = {
            AgentType.ORGANIZER: OrganizerAgent,
            AgentType.CLEANUP: CleanupAgent,
            AgentType.RESEARCH: ResearchAgent,
            AgentType.REPAIR: RepairAgent,
            AgentType.MONITOR: MonitorAgent,
            AgentType.SEARCH: SearchAgent,
        }

        agent_class = agent_classes.get(agent_type)
        if agent_class:
            self._agents[agent_type] = agent_class(config, self.ai_manager)
            logger.info(f"Registered agent: {agent_type.value}")

    def register_all_agents(self) -> None:
        """Register all available agents with default configs."""
        for agent_type in AgentType:
            if agent_type not in self._agents:
                self.register_agent(agent_type)

    async def submit_task(self, task: AgentTask) -> str:
        """Submit a task to the queue."""
        # Priority queue uses (priority, task) tuples
        # Negative priority so higher priority = processed first
        await self._task_queue.put((-task.priority.value, task))
        logger.info(f"Task {task.id} submitted", type=task.type.value, priority=task.priority.value)
        return task.id

    async def _worker(self, worker_id: int) -> None:
        """Worker coroutine that processes tasks from the queue."""
        logger.debug(f"Worker {worker_id} started")

        while self._running:
            try:
                # Get task with timeout to allow checking _running flag
                try:
                    _, task = await asyncio.wait_for(
                        self._task_queue.get(),
                        timeout=1.0
                    )
                except asyncio.TimeoutError:
                    continue

                agent = self._agents.get(task.type)
                if agent is None:
                    logger.error(f"No agent registered for type: {task.type.value}")
                    task.status = AgentStatus.FAILED
                    task.error = f"No agent for type: {task.type.value}"
                    continue

                await agent.run_task(task)
                self._task_queue.task_done()

            except asyncio.CancelledError:
                break
            except Exception as e:
                logger.exception(f"Worker {worker_id} error: {e}")

        logger.debug(f"Worker {worker_id} stopped")

    async def start(self, num_workers: int = 5) -> None:
        """Start the orchestrator with worker pool."""
        if self._running:
            return

        self._running = True
        self._max_workers = num_workers

        # Start worker tasks
        for i in range(num_workers):
            worker = asyncio.create_task(self._worker(i))
            self._workers.append(worker)

        logger.info(f"Orchestrator started with {num_workers} workers")

    async def stop(self) -> None:
        """Stop the orchestrator."""
        self._running = False

        # Cancel all workers
        for worker in self._workers:
            worker.cancel()

        # Wait for workers to finish
        await asyncio.gather(*self._workers, return_exceptions=True)
        self._workers.clear()

        logger.info("Orchestrator stopped")

    async def execute_task(self, task: AgentTask) -> AgentTask:
        """Execute a task immediately (bypassing queue)."""
        agent = self._agents.get(task.type)
        if agent is None:
            task.status = AgentStatus.FAILED
            task.error = f"No agent for type: {task.type.value}"
            return task

        return await agent.run_task(task)

    def get_agent(self, agent_type: AgentType) -> Optional[Agent]:
        """Get a registered agent."""
        return self._agents.get(agent_type)

    def get_status(self) -> Dict[str, Any]:
        """Get orchestrator status."""
        return {
            "running": self._running,
            "workers": len(self._workers),
            "queue_size": self._task_queue.qsize(),
            "agents": {
                t.value: {
                    "status": a.status.value,
                    "tasks_completed": len(a._task_history),
                }
                for t, a in self._agents.items()
            },
        }


# Global orchestrator instance
_orchestrator: Optional[AgentOrchestrator] = None


def get_orchestrator() -> AgentOrchestrator:
    """Get the global agent orchestrator."""
    global _orchestrator
    if _orchestrator is None:
        _orchestrator = AgentOrchestrator()
        _orchestrator.register_all_agents()
    return _orchestrator


async def quick_task(
    agent_type: AgentType,
    parameters: Dict[str, Any],
    description: str = "",
) -> Dict[str, Any]:
    """Convenience function to run a quick task."""
    orchestrator = get_orchestrator()
    task = AgentTask(
        type=agent_type,
        description=description,
        parameters=parameters,
    )
    result = await orchestrator.execute_task(task)
    return result.result or {"error": result.error}
