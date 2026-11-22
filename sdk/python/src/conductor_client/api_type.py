"""Conductor API types and the virtual model runner compatibility table."""

from __future__ import annotations

from enum import Enum


class ApiType(str, Enum):
    """Upstream API type of an endpoint, or the client-facing API type of a VMR.

    Member values are the wire names the server accepts and returns.
    ``number`` gives the integer stored by the server (never renumbered).
    """

    OLLAMA = "Ollama"
    OPENAI = "OpenAI"
    GEMINI = "Gemini"
    VLLM = "vLLM"
    ANTHROPIC = "Anthropic"
    TYPESAFE = "TypeSafe"
    OLLAYA = "Ollaya"
    OLLAMA_CLOUD = "OllamaCloud"
    OPENAI_COMPATIBLE = "OpenAICompatible"

    @property
    def number(self) -> int:
        """Integer value of this type in the server's ApiTypeEnum."""
        return _API_TYPE_NUMBERS[self]

    @classmethod
    def from_number(cls, number: int) -> "ApiType":
        """Return the API type for an integer value; raises ValueError when unknown."""
        for api_type, value in _API_TYPE_NUMBERS.items():
            if value == number:
                return api_type
        raise ValueError(f"Unknown ApiType value: {number}")

    def __str__(self) -> str:
        return self.value


_API_TYPE_NUMBERS: dict[ApiType, int] = {
    ApiType.OLLAMA: 0,
    ApiType.OPENAI: 1,
    ApiType.GEMINI: 2,
    ApiType.VLLM: 3,
    ApiType.ANTHROPIC: 4,
    ApiType.TYPESAFE: 5,
    ApiType.OLLAYA: 6,
    ApiType.OLLAMA_CLOUD: 7,
    ApiType.OPENAI_COMPATIBLE: 8,
}

VMR_ENDPOINT_COMPATIBILITY: dict[ApiType, tuple[ApiType, ...]] = {
    ApiType.OLLAMA: (ApiType.OLLAMA, ApiType.OLLAMA_CLOUD),
    ApiType.OPENAI: (ApiType.OPENAI, ApiType.OPENAI_COMPATIBLE, ApiType.VLLM),
    ApiType.GEMINI: (ApiType.GEMINI,),
    ApiType.VLLM: (ApiType.VLLM,),
    ApiType.ANTHROPIC: (ApiType.ANTHROPIC,),
    ApiType.TYPESAFE: (ApiType.TYPESAFE, ApiType.OLLAYA),
}
"""Endpoint API types accepted by each client-facing VMR type."""

CLIENT_FACING_API_TYPES: tuple[ApiType, ...] = tuple(
    sorted(VMR_ENDPOINT_COMPATIBILITY.keys(), key=lambda api_type: api_type.number)
)
"""API types that may be used as a VMR type, in enum order."""


def accepts_endpoint(vmr_api_type: ApiType | str, endpoint_api_type: ApiType | str) -> bool:
    """Return True when a VMR of ``vmr_api_type`` accepts an endpoint of ``endpoint_api_type``."""
    try:
        vmr_type = ApiType(vmr_api_type)
        endpoint_type = ApiType(endpoint_api_type)
    except ValueError:
        return False
    return endpoint_type in VMR_ENDPOINT_COMPATIBILITY.get(vmr_type, ())
