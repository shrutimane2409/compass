"""Free-tier chat models with automatic fallback: Groq first (fast), Gemini second."""
from langchain_core.language_models import BaseChatModel
from langchain_core.runnables import Runnable
from langchain_core.tools import BaseTool
from langchain_google_genai import ChatGoogleGenerativeAI
from langchain_groq import ChatGroq

from .config import get_settings


class LLMUnavailable(RuntimeError):
    """No LLM provider is configured."""


def chat_models() -> list[BaseChatModel]:
    s = get_settings()
    models: list[BaseChatModel] = []
    if s.groq_api_key:
        models.append(ChatGroq(
            model=s.groq_model, api_key=s.groq_api_key, temperature=0.2,
            timeout=s.llm_timeout_seconds, max_retries=1,
        ))
    if s.gemini_api_key:
        models.append(ChatGoogleGenerativeAI(
            model=s.gemini_model, google_api_key=s.gemini_api_key, temperature=0.2,
            timeout=s.llm_timeout_seconds, max_retries=1,
        ))
    if not models:
        raise LLMUnavailable("Set GROQ_API_KEY and/or GEMINI_API_KEY.")
    return models


def with_tools(tools: list[BaseTool]) -> Runnable:
    bound = [m.bind_tools(tools) for m in chat_models()]
    return bound[0].with_fallbacks(bound[1:]) if len(bound) > 1 else bound[0]
