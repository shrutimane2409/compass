"""Load living costs and one-off costs from CSV (upsert).

Usage (from ai/):  python -m scripts.load_costs ../data/living_costs.csv ../data/one_off_costs.csv
"""
import csv
import sys
from pathlib import Path

from app.db import get_pool


def is_true(v: str) -> bool:
    return (v or "").strip().lower() == "true"


def main(living_path: str, one_off_path: str) -> None:
    living = list(csv.DictReader(Path(living_path).open(encoding="utf-8")))
    one_off = list(csv.DictReader(Path(one_off_path).open(encoding="utf-8")))
    with get_pool().connection() as conn, conn.transaction():
        for r in living:
            conn.execute("""
                INSERT INTO living_costs (city, category, monthly_eur, source_url, verified_at, is_estimate)
                VALUES (%s, %s, %s, %s, %s, %s)
                ON CONFLICT (city, category) DO UPDATE SET monthly_eur = EXCLUDED.monthly_eur,
                    source_url = EXCLUDED.source_url, verified_at = EXCLUDED.verified_at, is_estimate = EXCLUDED.is_estimate
            """, (r["city"], r["category"], float(r["monthly_eur"]), r["source_url"], r["verified_at"], is_true(r["is_estimate"])))
        for r in one_off:
            conn.execute("""
                INSERT INTO one_off_costs (country, code, description, amount_eur, source_url, verified_at, is_estimate)
                VALUES (%s, %s, %s, %s, %s, %s, %s)
                ON CONFLICT (country, code) DO UPDATE SET description = EXCLUDED.description, amount_eur = EXCLUDED.amount_eur,
                    source_url = EXCLUDED.source_url, verified_at = EXCLUDED.verified_at, is_estimate = EXCLUDED.is_estimate
            """, (r["country"], r["code"], r["description"], float(r["amount_eur"]), r["source_url"], r["verified_at"],
                  is_true(r["is_estimate"])))
    print(f"Loaded {len(living)} living-cost rows and {len(one_off)} one-off rows.")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "../data/living_costs.csv",
         sys.argv[2] if len(sys.argv) > 2 else "../data/one_off_costs.csv")
