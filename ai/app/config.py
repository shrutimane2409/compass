from functools import lru_cache

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", extra="ignore")

    database_url: str = "postgresql://compass:compass_dev@localhost:5433/compass"
    compass_api_url: str = "http://localhost:5080"

    # Free-tier LLM providers. Groq is primary (fast); Gemini is the fallback.
    groq_api_key: str = ""
    groq_model: str = "llama-3.3-70b-versatile"
    gemini_api_key: str = ""
    gemini_model: str = "gemini-2.5-flash"
    llm_timeout_seconds: float = 20.0

    embedding_model: str = "BAAI/bge-small-en-v1.5"  # 384 dimensions, matches the schema

    # Cosine similarity range mapped to a 0..1 fit score
    fit_similarity_low: float = 0.35
    fit_similarity_high: float = 0.85

    agent_max_steps: int = 4
    retrieval_top_k: int = 6


@lru_cache
def get_settings() -> Settings:
    return Settings()
