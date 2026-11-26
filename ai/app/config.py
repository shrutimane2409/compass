from functools import lru_cache

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", extra="ignore")

    database_url: str = "postgresql://compass:compass_dev@localhost:5433/compass"
    compass_api_url: str = "http://localhost:5080"
    # When set, every endpoint except /health requires this value in the X-Internal-Key header.
    internal_api_key: str = ""

    # Free-tier LLM providers. Groq is primary (fast); Gemini is the fallback.
    groq_api_key: str = ""
    groq_model: str = "openai/gpt-oss-120b"
    gemini_api_key: str = ""
    gemini_model: str = "gemini-3.1-flash-lite"
    llm_timeout_seconds: float = 20.0

    embedding_model: str = "models/gemini-embedding-001"  # requested at 768 dimensions

    # Cosine similarity range mapped to a 0..1 fit score (calibrated for the embedding model in use)
    fit_similarity_low: float = 0.45
    fit_similarity_high: float = 0.80

    agent_max_steps: int = 4
    retrieval_top_k: int = 6


@lru_cache
def get_settings() -> Settings:
    return Settings()
