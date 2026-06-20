from __future__ import annotations

from dataclasses import dataclass
from enum import StrEnum


class ModelCapability(StrEnum):
    CODE_PRODUCTION = "code_production"
    CRITICAL_REVIEW = "critical_review"
    DOCUMENT_EXTRACTION = "document_extraction"
    EMBEDDINGS = "embeddings"
    EMERGENCY_FALLBACK = "emergency_fallback"


@dataclass(frozen=True, slots=True)
class ModelRoute:
    capability: ModelCapability
    model: str
    context_tokens: int
    supports_tools: bool = False
    supports_structured_output: bool = True
    runtime: str = "ollama"


class ModelGateway:
    def __init__(self, routes: tuple[ModelRoute, ...]) -> None:
        self._routes = {route.capability: route for route in routes}

    def resolve(self, capability: ModelCapability | str) -> ModelRoute:
        resolved = ModelCapability(capability)
        try:
            return self._routes[resolved]
        except KeyError as error:
            raise KeyError(f"no model route configured for {resolved}") from error


DEFAULT_OLLAMA_ROUTES = (
    ModelRoute(ModelCapability.CODE_PRODUCTION, "qwen3.6:27b", 65536, supports_tools=True),
    ModelRoute(ModelCapability.CRITICAL_REVIEW, "gemma4:26b", 32768),
    ModelRoute(ModelCapability.DOCUMENT_EXTRACTION, "granite4.1:30b", 32768),
    ModelRoute(ModelCapability.EMBEDDINGS, "bge-m3:latest", 8192, supports_tools=False),
    ModelRoute(ModelCapability.EMERGENCY_FALLBACK, "qwen3.6:27b", 8192),
)

