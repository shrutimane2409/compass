"""Load curated courses from data/courses.csv into PostgreSQL and embed their descriptions.

Usage (from ai/):  python -m scripts.load_courses ../data/courses.csv
Rows are upserted on (university, title). Every row must have source_url and verified_at.
english_source_url is optional: set it when the English minimums come from a university-wide page.
entry_source_url / grade_source_url are optional: the entry-requirements page and the India grade conversion page.
backgrounds_open = true when the page also accepts disciplines related to the ones it names.
"""
import csv
import sys
from pathlib import Path

from app.db import get_pool
from app.embeddings import embed_passages

REQUIRED = ["university_short_name", "title", "award", "field", "duration_months", "intake_month",
            "fee_non_eu_eur", "source_url", "verified_at"]


def none_if_blank(v: str | None):
    return v.strip() if v and v.strip() else None


def course_text(row: dict) -> str:
    return f"{row['title']}. {row.get('description', '')} Careers: {row.get('career_outcomes', '')}".strip()


def validate(row: dict, line: int) -> None:
    missing = [k for k in REQUIRED if not none_if_blank(row.get(k))]
    if missing:
        raise ValueError(f"Line {line}: missing {', '.join(missing)}")


def main(path: str) -> None:
    rows = list(csv.DictReader(Path(path).open(encoding="utf-8")))
    for i, row in enumerate(rows, start=2):
        validate(row, i)
    vectors = embed_passages([course_text(r) for r in rows])

    sql = """
        INSERT INTO courses (university_id, title, award, field, duration_months, intake_month, fee_non_eu_eur, fee_academic_year,
            application_deadline, min_grade_percent, accepted_backgrounds, min_ielts, min_pte, min_toefl, min_toefl_new, english_source_url,
            backgrounds_open, entry_source_url, grade_source_url, min_work_years, description, career_outcomes, source_url, verified_at, is_estimate, embedding)
        SELECT u.id, %(title)s, %(award)s, %(field)s, %(duration_months)s, %(intake_month)s, %(fee)s, %(fee_year)s,
            %(deadline)s, %(min_grade)s, %(backgrounds)s, %(ielts)s, %(pte)s, %(toefl)s, %(toefl_new)s, %(english_source)s,
            %(bg_open)s, %(entry_source)s, %(grade_source)s, %(work)s, %(description)s, %(careers)s, %(source_url)s, %(verified_at)s, %(estimate)s, %(embedding)s
        FROM universities u WHERE u.short_name = %(uni)s
        ON CONFLICT (university_id, title) DO UPDATE SET
            award = EXCLUDED.award, field = EXCLUDED.field, duration_months = EXCLUDED.duration_months,
            intake_month = EXCLUDED.intake_month, fee_non_eu_eur = EXCLUDED.fee_non_eu_eur,
            fee_academic_year = EXCLUDED.fee_academic_year,
            application_deadline = EXCLUDED.application_deadline, min_grade_percent = EXCLUDED.min_grade_percent,
            accepted_backgrounds = EXCLUDED.accepted_backgrounds, min_ielts = EXCLUDED.min_ielts,
            min_pte = EXCLUDED.min_pte, min_toefl = EXCLUDED.min_toefl, min_toefl_new = EXCLUDED.min_toefl_new, english_source_url = EXCLUDED.english_source_url,
            backgrounds_open = EXCLUDED.backgrounds_open, entry_source_url = EXCLUDED.entry_source_url,
            grade_source_url = EXCLUDED.grade_source_url, min_work_years = EXCLUDED.min_work_years,
            description = EXCLUDED.description, career_outcomes = EXCLUDED.career_outcomes,
            source_url = EXCLUDED.source_url, verified_at = EXCLUDED.verified_at,
            is_estimate = EXCLUDED.is_estimate, embedding = EXCLUDED.embedding
    """
    with get_pool().connection() as conn:
        for row, vec in zip(rows, vectors):
            cur = conn.execute(sql, {
                "uni": row["university_short_name"].strip(),
                "title": row["title"].strip(), "award": row["award"].strip(), "field": row["field"].strip(),
                "duration_months": int(row["duration_months"]), "intake_month": int(row["intake_month"]),
                "fee": float(row["fee_non_eu_eur"]), "fee_year": none_if_blank(row.get("fee_academic_year")),
                "deadline": none_if_blank(row.get("application_deadline")),
                "min_grade": none_if_blank(row.get("min_grade_percent")),
                "backgrounds": [b.strip() for b in (row.get("accepted_backgrounds") or "").split("|") if b.strip()],
                "ielts": none_if_blank(row.get("min_ielts")), "pte": none_if_blank(row.get("min_pte")),
                "toefl": none_if_blank(row.get("min_toefl")), "toefl_new": none_if_blank(row.get("min_toefl_new")), "english_source": none_if_blank(row.get("english_source_url")),
                "bg_open": (row.get("backgrounds_open") or "").strip().lower() == "true",
                "entry_source": none_if_blank(row.get("entry_source_url")),
                "grade_source": none_if_blank(row.get("grade_source_url")), "work": none_if_blank(row.get("min_work_years")) or 0,
                "description": row.get("description", ""), "careers": row.get("career_outcomes", ""),
                "source_url": row["source_url"].strip(), "verified_at": row["verified_at"].strip(),
                "estimate": (row.get("is_estimate") or "").strip().lower() == "true", "embedding": vec,
            })
            if cur.rowcount == 0:
                raise ValueError(f"Unknown university '{row['university_short_name']}' for '{row['title']}'")
    print(f"Loaded {len(rows)} courses.")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "../data/courses.csv")
