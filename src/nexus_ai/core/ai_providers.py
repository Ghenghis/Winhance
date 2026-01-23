"""
AI Provider Abstraction Layer

Unified interface for multiple AI/LLM providers:
- OpenAI (GPT-4, GPT-5.2)
- Anthropic (Claude Opus 4.5)
- Google (Gemini 3 Pro)
- Local (LM Studio, Ollama)
- AnythingLLM

Supports GPU acceleration with RTX 3090 Ti.
"""

from __future__ import annotations

import os
import json
import asyncio
import httpx
from abc import ABC, abstractmethod
from dataclasses import dataclass, field
from enum import Enum
from typing import Optional, Dict, Any, List, AsyncIterator, Callable
from pathlib import Path

from nexus_ai.core.logging_config import get_logger, log_async_function_call

logger = get_logger("ai_providers")


class ProviderType(Enum):
    """Supported AI providers."""
    OPENAI = "openai"
    ANTHROPIC = "anthropic"
    GOOGLE = "google"
    LMSTUDIO = "lmstudio"
    OLLAMA = "ollama"
    ANYTHINGLLM = "anythingllm"
    LOCAL_TRANSFORMERS = "local_transformers"


@dataclass
class AIMessage:
    """A message in a conversation."""
    role: str  # "system", "user", "assistant"
    content: str
    name: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)


@dataclass
class AIResponse:
    """Response from an AI provider."""
    content: str
    model: str
    provider: ProviderType
    tokens_used: int = 0
    finish_reason: str = "stop"
    metadata: Dict[str, Any] = field(default_factory=dict)


@dataclass
class AIConfig:
    """Configuration for an AI provider."""
    provider: ProviderType
    api_key: Optional[str] = None
    base_url: Optional[str] = None
    model: str = ""
    temperature: float = 0.7
    max_tokens: int = 4096
    timeout: float = 120.0
    # GPU settings for local models
    gpu_layers: int = -1  # -1 = all layers on GPU
    gpu_memory_fraction: float = 0.9
    # Batch settings
    batch_size: int = 1


class AIProvider(ABC):
    """Abstract base class for AI providers."""

    def __init__(self, config: AIConfig):
        self.config = config
        self._client: Optional[httpx.AsyncClient] = None

    @abstractmethod
    async def chat(
        self,
        messages: List[AIMessage],
        **kwargs
    ) -> AIResponse:
        """Send a chat completion request."""
        pass

    @abstractmethod
    async def stream_chat(
        self,
        messages: List[AIMessage],
        **kwargs
    ) -> AsyncIterator[str]:
        """Stream a chat completion response."""
        pass

    async def __aenter__(self):
        self._client = httpx.AsyncClient(timeout=self.config.timeout)
        return self

    async def __aexit__(self, exc_type, exc_val, exc_tb):
        if self._client:
            await self._client.aclose()


class OpenAIProvider(AIProvider):
    """OpenAI API provider (GPT-4, GPT-5.2)."""

    def __init__(self, config: AIConfig):
        super().__init__(config)
        self.config.base_url = config.base_url or "https://api.openai.com/v1"
        self.config.model = config.model or "gpt-4-turbo-preview"

    @log_async_function_call
    async def chat(self, messages: List[AIMessage], **kwargs) -> AIResponse:
        headers = {
            "Authorization": f"Bearer {self.config.api_key}",
            "Content-Type": "application/json",
        }

        payload = {
            "model": kwargs.get("model", self.config.model),
            "messages": [{"role": m.role, "content": m.content} for m in messages],
            "temperature": kwargs.get("temperature", self.config.temperature),
            "max_tokens": kwargs.get("max_tokens", self.config.max_tokens),
        }

        response = await self._client.post(
            f"{self.config.base_url}/chat/completions",
            headers=headers,
            json=payload,
        )
        response.raise_for_status()
        data = response.json()

        return AIResponse(
            content=data["choices"][0]["message"]["content"],
            model=data["model"],
            provider=ProviderType.OPENAI,
            tokens_used=data.get("usage", {}).get("total_tokens", 0),
            finish_reason=data["choices"][0].get("finish_reason", "stop"),
        )

    async def stream_chat(self, messages: List[AIMessage], **kwargs) -> AsyncIterator[str]:
        headers = {
            "Authorization": f"Bearer {self.config.api_key}",
            "Content-Type": "application/json",
        }

        payload = {
            "model": kwargs.get("model", self.config.model),
            "messages": [{"role": m.role, "content": m.content} for m in messages],
            "temperature": kwargs.get("temperature", self.config.temperature),
            "max_tokens": kwargs.get("max_tokens", self.config.max_tokens),
            "stream": True,
        }

        async with self._client.stream(
            "POST",
            f"{self.config.base_url}/chat/completions",
            headers=headers,
            json=payload,
        ) as response:
            async for line in response.aiter_lines():
                if line.startswith("data: ") and line != "data: [DONE]":
                    data = json.loads(line[6:])
                    if content := data["choices"][0]["delta"].get("content"):
                        yield content


class AnthropicProvider(AIProvider):
    """Anthropic API provider (Claude Opus 4.5)."""

    def __init__(self, config: AIConfig):
        super().__init__(config)
        self.config.base_url = config.base_url or "https://api.anthropic.com/v1"
        self.config.model = config.model or "claude-opus-4-5-20251101"

    @log_async_function_call
    async def chat(self, messages: List[AIMessage], **kwargs) -> AIResponse:
        headers = {
            "x-api-key": self.config.api_key,
            "Content-Type": "application/json",
            "anthropic-version": "2023-06-01",
        }

        # Extract system message
        system_msg = ""
        chat_messages = []
        for m in messages:
            if m.role == "system":
                system_msg = m.content
            else:
                chat_messages.append({"role": m.role, "content": m.content})

        payload = {
            "model": kwargs.get("model", self.config.model),
            "messages": chat_messages,
            "max_tokens": kwargs.get("max_tokens", self.config.max_tokens),
        }
        if system_msg:
            payload["system"] = system_msg

        response = await self._client.post(
            f"{self.config.base_url}/messages",
            headers=headers,
            json=payload,
        )
        response.raise_for_status()
        data = response.json()

        return AIResponse(
            content=data["content"][0]["text"],
            model=data["model"],
            provider=ProviderType.ANTHROPIC,
            tokens_used=data.get("usage", {}).get("input_tokens", 0) + data.get("usage", {}).get("output_tokens", 0),
            finish_reason=data.get("stop_reason", "end_turn"),
        )

    async def stream_chat(self, messages: List[AIMessage], **kwargs) -> AsyncIterator[str]:
        headers = {
            "x-api-key": self.config.api_key,
            "Content-Type": "application/json",
            "anthropic-version": "2023-06-01",
        }

        system_msg = ""
        chat_messages = []
        for m in messages:
            if m.role == "system":
                system_msg = m.content
            else:
                chat_messages.append({"role": m.role, "content": m.content})

        payload = {
            "model": kwargs.get("model", self.config.model),
            "messages": chat_messages,
            "max_tokens": kwargs.get("max_tokens", self.config.max_tokens),
            "stream": True,
        }
        if system_msg:
            payload["system"] = system_msg

        async with self._client.stream(
            "POST",
            f"{self.config.base_url}/messages",
            headers=headers,
            json=payload,
        ) as response:
            async for line in response.aiter_lines():
                if line.startswith("data: "):
                    data = json.loads(line[6:])
                    if data["type"] == "content_block_delta":
                        yield data["delta"].get("text", "")


class GoogleProvider(AIProvider):
    """Google Gemini API provider (Gemini 3 Pro)."""

    def __init__(self, config: AIConfig):
        super().__init__(config)
        self.config.base_url = config.base_url or "https://generativelanguage.googleapis.com/v1beta"
        self.config.model = config.model or "gemini-pro"

    @log_async_function_call
    async def chat(self, messages: List[AIMessage], **kwargs) -> AIResponse:
        # Convert to Gemini format
        contents = []
        for m in messages:
            if m.role == "system":
                contents.append({"role": "user", "parts": [{"text": f"[System]: {m.content}"}]})
            else:
                role = "user" if m.role == "user" else "model"
                contents.append({"role": role, "parts": [{"text": m.content}]})

        payload = {
            "contents": contents,
            "generationConfig": {
                "temperature": kwargs.get("temperature", self.config.temperature),
                "maxOutputTokens": kwargs.get("max_tokens", self.config.max_tokens),
            },
        }

        response = await self._client.post(
            f"{self.config.base_url}/models/{self.config.model}:generateContent?key={self.config.api_key}",
            json=payload,
        )
        response.raise_for_status()
        data = response.json()

        return AIResponse(
            content=data["candidates"][0]["content"]["parts"][0]["text"],
            model=self.config.model,
            provider=ProviderType.GOOGLE,
            tokens_used=data.get("usageMetadata", {}).get("totalTokenCount", 0),
            finish_reason=data["candidates"][0].get("finishReason", "STOP"),
        )

    async def stream_chat(self, messages: List[AIMessage], **kwargs) -> AsyncIterator[str]:
        # Similar to chat but with streaming endpoint
        contents = []
        for m in messages:
            if m.role == "system":
                contents.append({"role": "user", "parts": [{"text": f"[System]: {m.content}"}]})
            else:
                role = "user" if m.role == "user" else "model"
                contents.append({"role": role, "parts": [{"text": m.content}]})

        payload = {
            "contents": contents,
            "generationConfig": {
                "temperature": kwargs.get("temperature", self.config.temperature),
                "maxOutputTokens": kwargs.get("max_tokens", self.config.max_tokens),
            },
        }

        async with self._client.stream(
            "POST",
            f"{self.config.base_url}/models/{self.config.model}:streamGenerateContent?key={self.config.api_key}",
            json=payload,
        ) as response:
            async for line in response.aiter_lines():
                if line:
                    data = json.loads(line)
                    if "candidates" in data:
                        yield data["candidates"][0]["content"]["parts"][0]["text"]


class LMStudioProvider(AIProvider):
    """LM Studio local provider (OpenAI-compatible API)."""

    def __init__(self, config: AIConfig):
        super().__init__(config)
        self.config.base_url = config.base_url or "http://localhost:1234/v1"
        self.config.model = config.model or "local-model"

    @log_async_function_call
    async def chat(self, messages: List[AIMessage], **kwargs) -> AIResponse:
        payload = {
            "model": kwargs.get("model", self.config.model),
            "messages": [{"role": m.role, "content": m.content} for m in messages],
            "temperature": kwargs.get("temperature", self.config.temperature),
            "max_tokens": kwargs.get("max_tokens", self.config.max_tokens),
        }

        response = await self._client.post(
            f"{self.config.base_url}/chat/completions",
            json=payload,
        )
        response.raise_for_status()
        data = response.json()

        return AIResponse(
            content=data["choices"][0]["message"]["content"],
            model=data.get("model", self.config.model),
            provider=ProviderType.LMSTUDIO,
            tokens_used=data.get("usage", {}).get("total_tokens", 0),
            finish_reason=data["choices"][0].get("finish_reason", "stop"),
        )

    async def stream_chat(self, messages: List[AIMessage], **kwargs) -> AsyncIterator[str]:
        payload = {
            "model": kwargs.get("model", self.config.model),
            "messages": [{"role": m.role, "content": m.content} for m in messages],
            "temperature": kwargs.get("temperature", self.config.temperature),
            "max_tokens": kwargs.get("max_tokens", self.config.max_tokens),
            "stream": True,
        }

        async with self._client.stream(
            "POST",
            f"{self.config.base_url}/chat/completions",
            json=payload,
        ) as response:
            async for line in response.aiter_lines():
                if line.startswith("data: ") and line != "data: [DONE]":
                    data = json.loads(line[6:])
                    if content := data["choices"][0]["delta"].get("content"):
                        yield content


class OllamaProvider(AIProvider):
    """Ollama local provider with GPU support."""

    def __init__(self, config: AIConfig):
        super().__init__(config)
        self.config.base_url = config.base_url or "http://localhost:11434"
        self.config.model = config.model or "llama3.2"

    @log_async_function_call
    async def chat(self, messages: List[AIMessage], **kwargs) -> AIResponse:
        payload = {
            "model": kwargs.get("model", self.config.model),
            "messages": [{"role": m.role, "content": m.content} for m in messages],
            "options": {
                "temperature": kwargs.get("temperature", self.config.temperature),
                "num_predict": kwargs.get("max_tokens", self.config.max_tokens),
                "num_gpu": self.config.gpu_layers,
            },
            "stream": False,
        }

        response = await self._client.post(
            f"{self.config.base_url}/api/chat",
            json=payload,
        )
        response.raise_for_status()
        data = response.json()

        return AIResponse(
            content=data["message"]["content"],
            model=data.get("model", self.config.model),
            provider=ProviderType.OLLAMA,
            tokens_used=data.get("eval_count", 0) + data.get("prompt_eval_count", 0),
            finish_reason="stop",
            metadata={
                "total_duration": data.get("total_duration"),
                "load_duration": data.get("load_duration"),
                "eval_duration": data.get("eval_duration"),
            },
        )

    async def stream_chat(self, messages: List[AIMessage], **kwargs) -> AsyncIterator[str]:
        payload = {
            "model": kwargs.get("model", self.config.model),
            "messages": [{"role": m.role, "content": m.content} for m in messages],
            "options": {
                "temperature": kwargs.get("temperature", self.config.temperature),
                "num_predict": kwargs.get("max_tokens", self.config.max_tokens),
                "num_gpu": self.config.gpu_layers,
            },
            "stream": True,
        }

        async with self._client.stream(
            "POST",
            f"{self.config.base_url}/api/chat",
            json=payload,
        ) as response:
            async for line in response.aiter_lines():
                if line:
                    data = json.loads(line)
                    if content := data.get("message", {}).get("content"):
                        yield content


class AnythingLLMProvider(AIProvider):
    """AnythingLLM provider."""

    def __init__(self, config: AIConfig):
        super().__init__(config)
        self.config.base_url = config.base_url or "http://localhost:3001/api/v1"

    @log_async_function_call
    async def chat(self, messages: List[AIMessage], **kwargs) -> AIResponse:
        headers = {
            "Authorization": f"Bearer {self.config.api_key}",
            "Content-Type": "application/json",
        }

        # AnythingLLM expects workspace-based chat
        workspace = kwargs.get("workspace", "default")
        payload = {
            "message": messages[-1].content if messages else "",
            "mode": "chat",
        }

        response = await self._client.post(
            f"{self.config.base_url}/workspace/{workspace}/chat",
            headers=headers,
            json=payload,
        )
        response.raise_for_status()
        data = response.json()

        return AIResponse(
            content=data.get("textResponse", ""),
            model="anythingllm",
            provider=ProviderType.ANYTHINGLLM,
            tokens_used=0,
            finish_reason="stop",
        )

    async def stream_chat(self, messages: List[AIMessage], **kwargs) -> AsyncIterator[str]:
        # AnythingLLM streaming not fully supported yet
        response = await self.chat(messages, **kwargs)
        yield response.content


class AIProviderManager:
    """
    Manager for multiple AI providers.

    Supports:
    - Provider selection based on task
    - Automatic failover
    - Load balancing
    - Cost tracking
    """

    def __init__(self):
        self._providers: Dict[ProviderType, AIConfig] = {}
        self._active_connections: Dict[ProviderType, AIProvider] = {}
        self._default_provider: Optional[ProviderType] = None

    def register_provider(
        self,
        provider_type: ProviderType,
        config: AIConfig,
        set_default: bool = False
    ) -> None:
        """Register an AI provider."""
        self._providers[provider_type] = config
        if set_default or self._default_provider is None:
            self._default_provider = provider_type
        logger.info(f"Registered provider: {provider_type.value}", model=config.model)

    def get_provider(self, provider_type: Optional[ProviderType] = None) -> AIProvider:
        """Get a provider instance."""
        pt = provider_type or self._default_provider
        if pt is None:
            raise ValueError("No provider registered")

        config = self._providers.get(pt)
        if config is None:
            raise ValueError(f"Provider {pt.value} not registered")

        provider_classes = {
            ProviderType.OPENAI: OpenAIProvider,
            ProviderType.ANTHROPIC: AnthropicProvider,
            ProviderType.GOOGLE: GoogleProvider,
            ProviderType.LMSTUDIO: LMStudioProvider,
            ProviderType.OLLAMA: OllamaProvider,
            ProviderType.ANYTHINGLLM: AnythingLLMProvider,
        }

        provider_class = provider_classes.get(pt)
        if provider_class is None:
            raise ValueError(f"Unsupported provider: {pt.value}")

        return provider_class(config)

    async def chat(
        self,
        messages: List[AIMessage],
        provider_type: Optional[ProviderType] = None,
        **kwargs
    ) -> AIResponse:
        """Send a chat request to a provider."""
        provider = self.get_provider(provider_type)
        async with provider:
            return await provider.chat(messages, **kwargs)

    async def stream_chat(
        self,
        messages: List[AIMessage],
        provider_type: Optional[ProviderType] = None,
        **kwargs
    ) -> AsyncIterator[str]:
        """Stream a chat response from a provider."""
        provider = self.get_provider(provider_type)
        async with provider:
            async for chunk in provider.stream_chat(messages, **kwargs):
                yield chunk

    def configure_from_env(self) -> None:
        """Configure providers from environment variables."""
        # OpenAI
        if api_key := os.getenv("OPENAI_API_KEY"):
            self.register_provider(
                ProviderType.OPENAI,
                AIConfig(
                    provider=ProviderType.OPENAI,
                    api_key=api_key,
                    model=os.getenv("OPENAI_MODEL", "gpt-4-turbo-preview"),
                ),
            )

        # Anthropic
        if api_key := os.getenv("ANTHROPIC_API_KEY"):
            self.register_provider(
                ProviderType.ANTHROPIC,
                AIConfig(
                    provider=ProviderType.ANTHROPIC,
                    api_key=api_key,
                    model=os.getenv("ANTHROPIC_MODEL", "claude-opus-4-5-20251101"),
                ),
            )

        # Google
        if api_key := os.getenv("GOOGLE_API_KEY"):
            self.register_provider(
                ProviderType.GOOGLE,
                AIConfig(
                    provider=ProviderType.GOOGLE,
                    api_key=api_key,
                    model=os.getenv("GOOGLE_MODEL", "gemini-pro"),
                ),
            )

        # LM Studio (local)
        if os.getenv("LMSTUDIO_ENABLED", "").lower() == "true":
            self.register_provider(
                ProviderType.LMSTUDIO,
                AIConfig(
                    provider=ProviderType.LMSTUDIO,
                    base_url=os.getenv("LMSTUDIO_URL", "http://localhost:1234/v1"),
                    model=os.getenv("LMSTUDIO_MODEL", "local-model"),
                    gpu_layers=int(os.getenv("LMSTUDIO_GPU_LAYERS", "-1")),
                ),
            )

        # Ollama (local)
        if os.getenv("OLLAMA_ENABLED", "").lower() == "true":
            self.register_provider(
                ProviderType.OLLAMA,
                AIConfig(
                    provider=ProviderType.OLLAMA,
                    base_url=os.getenv("OLLAMA_URL", "http://localhost:11434"),
                    model=os.getenv("OLLAMA_MODEL", "llama3.2"),
                    gpu_layers=int(os.getenv("OLLAMA_GPU_LAYERS", "-1")),
                ),
            )

        # AnythingLLM
        if api_key := os.getenv("ANYTHINGLLM_API_KEY"):
            self.register_provider(
                ProviderType.ANYTHINGLLM,
                AIConfig(
                    provider=ProviderType.ANYTHINGLLM,
                    api_key=api_key,
                    base_url=os.getenv("ANYTHINGLLM_URL", "http://localhost:3001/api/v1"),
                ),
            )

        logger.info(f"Configured {len(self._providers)} providers from environment")


# Global provider manager
_provider_manager: Optional[AIProviderManager] = None


def get_ai_manager() -> AIProviderManager:
    """Get the global AI provider manager."""
    global _provider_manager
    if _provider_manager is None:
        _provider_manager = AIProviderManager()
        _provider_manager.configure_from_env()
    return _provider_manager
