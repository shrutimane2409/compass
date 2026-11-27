"""Load university rankings and scholarships from CSV (upsert). Unknown universities are reported, not guessed.

Usage (from ai/):  python -m scripts.load_extras ../data/rankings.csv ../data/scholarships.csv
"""
import csv
import sys
from pathlib import Path

from app.db import get_pool


def is_true(v: str) -> bool:
    return (v or "").strip().lower() == "true"


def num(v: str):
    v = (v or "").strip()
    return float(v) if v else None


def main(rankings_path: str, scholarships_path: str) -> None:
    rankings = list(csv.DictReader(Path(rankings_path).open(encoding="utf-8")))
    scholarships = list(csv.DictReader(Path(scholarships_path).open(encoding="utf-8")))
    with get_pool().connection() as conn, conn.transaction():
        uni = {short: uid for uid, short in conn.execute("SELECT id, short_name FROM universities").fetchall()}

        def uni_id(short: str):
            short = (short or "").strip()
            if not short:
                return None
            if short not in uni:
                raise SystemExit(f"Unknown university short name: {short!r}")
            return uni[short]

        for r in rankings:
            conn.execute("""
                INSERT INTO university_rankings (university_id, source, edition, scope, rank_text, rank_low, source_url, verified_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s)
                ON CONFLICT (university_id, source, edition, scope) DO UPDATE SET rank_text = EXCLUDED.rank_text,
                    rank_low = EXCLUDED.rank_low, source_url = EXCLUDED.source_url, verified_at = EXCLUDED.verified_at
            """, (uni_id(r["university_short_name"]), r["source"], int(r["edition"]), r["scope"], r["rank_text"],
                  int(r["rank_low"]), r["source_url"], r["verified_at"]))

        for s in scholarships:
            fields = [f for f in (s["fields"] or "").split(";") if f]
            conn.execute("""
                INSERT INTO scholarships (slug, name, provider, university_id, amount_text, max_value_eur, fields,
                    min_grade_percent, requires_offer, automatic, competitive, nationality, how_to_apply, deadline_text,
                    source_url, verified_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                ON CONFLICT (slug) DO UPDATE SET name = EXCLUDED.name, provider = EXCLUDED.provider,
                    university_id = EXCLUDED.university_id, amount_text = EXCLUDED.amount_text,
                    max_value_eur = EXCLUDED.max_value_eur, fields = EXCLUDED.fields,
                    min_grade_percent = EXCLUDED.min_grade_percent, requires_offer = EXCLUDED.requires_offer,
                    automatic = EXCLUDED.automatic, competitive = EXCLUDED.competitive,
                    nationality = EXCLUDED.nationality, how_to_apply = EXCLUDED.how_to_apply,
                    deadline_text = EXCLUDED.deadline_text, source_url = EXCLUDED.source_url,
                    verified_at = EXCLUDED.verified_at
            """, (s["slug"], s["name"], s["provider"], uni_id(s["university_short_name"]), s["amount_text"],
                  num(s["max_value_eur"]), fields, num(s["min_grade_percent"]), is_true(s["requires_offer"]),
                  is_true(s["automatic"]), is_true(s["competitive"]), s["nationality"], s["how_to_apply"],
                  s["deadline_text"], s["source_url"], s["verified_at"]))
    print(f"Loaded {len(rankings)} ranking rows and {len(scholarships)} scholarships.")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "../data/rankings.csv",
         sys.argv[2] if len(sys.argv) > 2 else "../data/scholarships.csv")
