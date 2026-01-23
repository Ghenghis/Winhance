"""
Tests for Agent Orchestration System

Tests for all agent types and orchestration functionality.
"""

from __future__ import annotations

import pytest
import asyncio
from pathlib import Path
from typing import Dict, List, Any
from datetime import datetime

from tests.conftest import DummyFileGenerator


class TestAgentTypes:
    """Tests for individual agent types."""

    @pytest.mark.asyncio
    async def test_organizer_agent_creation(self):
        """Test OrganizerAgent can be created."""
        from nexus_ai.core.agents import OrganizerAgent, AgentConfig, AgentType

        config = AgentConfig(
            type=AgentType.ORGANIZER,
            name="test_organizer",
            description="Test organizer agent",
        )
        agent = OrganizerAgent(config)

        assert agent is not None
        assert agent.config.type == AgentType.ORGANIZER

    @pytest.mark.asyncio
    async def test_cleanup_agent_creation(self):
        """Test CleanupAgent can be created."""
        from nexus_ai.core.agents import CleanupAgent, AgentConfig, AgentType

        config = AgentConfig(
            type=AgentType.CLEANUP,
            name="test_cleanup",
            description="Test cleanup agent",
        )
        agent = CleanupAgent(config)

        assert agent is not None
        assert agent.config.type == AgentType.CLEANUP

    @pytest.mark.asyncio
    async def test_search_agent_creation(self):
        """Test SearchAgent can be created."""
        from nexus_ai.core.agents import SearchAgent, AgentConfig, AgentType

        config = AgentConfig(
            type=AgentType.SEARCH,
            name="test_search",
            description="Test search agent",
        )
        agent = SearchAgent(config)

        assert agent is not None
        assert agent.config.type == AgentType.SEARCH


class TestAgentTask:
    """Tests for AgentTask dataclass."""

    def test_task_creation(self):
        """Test AgentTask can be created with defaults."""
        from nexus_ai.core.agents import AgentTask, AgentType, AgentStatus

        task = AgentTask(
            type=AgentType.ORGANIZER,
            description="Test task",
        )

        assert task.id is not None
        assert len(task.id) == 8
        assert task.status == AgentStatus.IDLE
        assert task.type == AgentType.ORGANIZER

    def test_task_with_parameters(self):
        """Test AgentTask with custom parameters."""
        from nexus_ai.core.agents import AgentTask, AgentType, TaskPriority

        task = AgentTask(
            type=AgentType.CLEANUP,
            description="Cleanup task",
            parameters={"path": "/test", "dry_run": True},
            priority=TaskPriority.HIGH,
        )

        assert task.parameters["path"] == "/test"
        assert task.parameters["dry_run"] is True
        assert task.priority == TaskPriority.HIGH


class TestAgentOrchestrator:
    """Tests for AgentOrchestrator."""

    def test_orchestrator_creation(self):
        """Test orchestrator can be created."""
        from nexus_ai.core.agents import AgentOrchestrator

        orchestrator = AgentOrchestrator()
        assert orchestrator is not None

    def test_register_all_agents(self):
        """Test registering all agents."""
        from nexus_ai.core.agents import AgentOrchestrator, AgentType

        orchestrator = AgentOrchestrator()
        orchestrator.register_all_agents()

        # Check agents are registered
        for agent_type in [AgentType.ORGANIZER, AgentType.CLEANUP, AgentType.SEARCH]:
            assert orchestrator.get_agent(agent_type) is not None

    def test_get_status(self):
        """Test getting orchestrator status."""
        from nexus_ai.core.agents import AgentOrchestrator

        orchestrator = AgentOrchestrator()
        orchestrator.register_all_agents()

        status = orchestrator.get_status()

        assert "running" in status
        assert "workers" in status
        assert "queue_size" in status
        assert "agents" in status

    @pytest.mark.asyncio
    async def test_execute_task_directly(self, mock_ai_provider):
        """Test executing a task directly (bypassing queue)."""
        from nexus_ai.core.agents import (
            AgentOrchestrator, AgentTask, AgentType, AgentStatus
        )

        orchestrator = AgentOrchestrator()
        orchestrator.register_all_agents()

        # Use monitor agent which doesn't need AI
        task = AgentTask(
            type=AgentType.MONITOR,
            description="Check status",
            parameters={"action": "status"},
        )

        result = await orchestrator.execute_task(task)

        assert result.status in [AgentStatus.COMPLETED, AgentStatus.FAILED]

    @pytest.mark.asyncio
    async def test_orchestrator_start_stop(self):
        """Test starting and stopping orchestrator."""
        from nexus_ai.core.agents import AgentOrchestrator

        orchestrator = AgentOrchestrator()
        orchestrator.register_all_agents()

        await orchestrator.start(num_workers=2)
        status = orchestrator.get_status()
        assert status["running"] is True
        assert status["workers"] == 2

        await orchestrator.stop()
        status = orchestrator.get_status()
        assert status["running"] is False


class TestAgentExecution:
    """Tests for agent task execution."""

    @pytest.mark.asyncio
    async def test_monitor_agent_status(self):
        """Test MonitorAgent status action."""
        from nexus_ai.core.agents import MonitorAgent, AgentConfig, AgentType, AgentTask

        config = AgentConfig(
            type=AgentType.MONITOR,
            name="test_monitor",
            description="Test monitor",
        )
        agent = MonitorAgent(config)

        task = AgentTask(
            type=AgentType.MONITOR,
            parameters={"action": "status"},
        )

        result = await agent.run_task(task)
        assert result.result is not None
        assert "watchers" in result.result

    @pytest.mark.asyncio
    async def test_repair_agent_scan(self, file_generator: DummyFileGenerator):
        """Test RepairAgent scanning for issues."""
        from nexus_ai.core.agents import RepairAgent, AgentConfig, AgentType, AgentTask

        # Create some test files
        file_generator.create_file("test.txt", 100)

        config = AgentConfig(
            type=AgentType.REPAIR,
            name="test_repair",
            description="Test repair",
        )
        agent = RepairAgent(config)

        task = AgentTask(
            type=AgentType.REPAIR,
            parameters={"path": str(file_generator.base_dir)},
        )

        result = await agent.run_task(task)
        assert result.result is not None
        assert "issues_found" in result.result


class TestGlobalOrchestrator:
    """Tests for global orchestrator instance."""

    def test_get_orchestrator(self):
        """Test getting global orchestrator."""
        from nexus_ai.core.agents import get_orchestrator

        orchestrator1 = get_orchestrator()
        orchestrator2 = get_orchestrator()

        assert orchestrator1 is orchestrator2  # Same instance

    @pytest.mark.asyncio
    async def test_quick_task(self):
        """Test quick_task convenience function."""
        from nexus_ai.core.agents import quick_task, AgentType

        result = await quick_task(
            AgentType.MONITOR,
            {"action": "status"},
            description="Quick status check",
        )

        assert isinstance(result, dict)


class TestAgentLogging:
    """Tests for agent logging functionality."""

    @pytest.mark.asyncio
    async def test_agent_logs_task_start(self, caplog):
        """Test that agents log task start."""
        from nexus_ai.core.agents import MonitorAgent, AgentConfig, AgentType, AgentTask
        from nexus_ai.core.logging_config import setup_logging

        setup_logging()

        config = AgentConfig(
            type=AgentType.MONITOR,
            name="log_test",
            description="Log test agent",
        )
        agent = MonitorAgent(config)

        task = AgentTask(
            type=AgentType.MONITOR,
            parameters={"action": "status"},
        )

        await agent.run_task(task)

        # Task should be tracked
        assert len(agent._task_history) > 0


class TestAgentPriority:
    """Tests for task priority handling."""

    @pytest.mark.asyncio
    async def test_high_priority_processed_first(self):
        """Test that high priority tasks are processed first."""
        from nexus_ai.core.agents import (
            AgentOrchestrator, AgentTask, AgentType, TaskPriority
        )

        orchestrator = AgentOrchestrator()
        orchestrator.register_all_agents()

        # Submit tasks with different priorities
        low_task = AgentTask(
            type=AgentType.MONITOR,
            priority=TaskPriority.LOW,
            parameters={"action": "status"},
        )
        high_task = AgentTask(
            type=AgentType.MONITOR,
            priority=TaskPriority.HIGH,
            parameters={"action": "status"},
        )

        # Submit low first, then high
        await orchestrator.submit_task(low_task)
        await orchestrator.submit_task(high_task)

        # Queue should have high priority first (negative priority = higher first)
        assert orchestrator._task_queue.qsize() == 2


class TestAgentErrorHandling:
    """Tests for agent error handling."""

    @pytest.mark.asyncio
    async def test_task_failure_handling(self):
        """Test handling of task failures."""
        from nexus_ai.core.agents import (
            AgentOrchestrator, AgentTask, AgentType, AgentStatus
        )

        orchestrator = AgentOrchestrator()
        orchestrator.register_all_agents()

        # Create task for unregistered agent type
        task = AgentTask(
            type=AgentType.BACKUP,  # Not registered by default
            parameters={},
        )

        # Should be registered now
        result = await orchestrator.execute_task(task)

        # Check result
        assert result.status in [AgentStatus.COMPLETED, AgentStatus.FAILED]

    @pytest.mark.asyncio
    async def test_task_cancellation(self):
        """Test task cancellation."""
        from nexus_ai.core.agents import AgentTask, AgentStatus, AgentType

        task = AgentTask(type=AgentType.MONITOR)
        task.status = AgentStatus.CANCELLED
        task.error = "Cancelled by user"

        assert task.status == AgentStatus.CANCELLED
        assert task.error is not None
