from functools import lru_cache

from pgvector.psycopg import register_vector
from psycopg_pool import ConnectionPool

from .config import get_settings


@lru_cache
def get_pool() -> ConnectionPool:
    return ConnectionPool(
        get_settings().database_url,
        min_size=1,
        max_size=5,
        configure=register_vector,
        open=True,
    )
