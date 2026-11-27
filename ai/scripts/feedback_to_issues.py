"""Open a GitHub issue for each new course_feedback row, then mark it notified.

Runs in GitHub Actions (feedback-triage.yml). Uses the gh CLI that Actions provides.
An issue is created before the row is marked, so a crash can at worst duplicate an issue, never lose a report.
"""
import os
import subprocess

import psycopg

SELECT = """
SELECT f.id, f.field, f.message, f.page_url, f.created_at, c.id, c.title, u.name, c.source_url
FROM course_feedback f
JOIN courses c ON c.id = f.course_id
JOIN universities u ON u.id = c.university_id
WHERE f.notified_at IS NULL AND f.status = 'open'
ORDER BY f.created_at
LIMIT 20
"""


def main() -> None:
    with psycopg.connect(os.environ["DATABASE_URL"]) as conn:
        rows = conn.execute(SELECT).fetchall()
        for fid, field, message, page_url, created, cid, course, uni, source in rows:
            title = f"Data report: {field} for {course} ({uni})"
            body = (
                f"**Course:** {course} - {uni} (id {cid})\n"
                f"**Field:** {field}\n**Reported:** {created:%Y-%m-%d %H:%M} UTC\n"
                f"**Official page:** {source}\n\n> {message.replace(chr(10), chr(10) + '> ')}\n\n"
                f"Feedback id {fid}. Verify against the official page, fix `data/courses.csv` if needed, "
                f"then set `course_feedback.status` to fixed or rejected."
            )
            _ensure_label()
            subprocess.run(["gh", "issue", "create", "--title", title, "--body", body, "--label", "data-report"], check=True)
            conn.execute("UPDATE course_feedback SET notified_at = now() WHERE id = %s", (fid,))
            conn.commit()
        print(f"Notified {len(rows)} report(s).")


_label_ready = False


def _ensure_label() -> None:
    global _label_ready
    if not _label_ready:
        subprocess.run(["gh", "label", "create", "data-report", "--color", "d93f0b", "--force"],
                       check=False, capture_output=True)
        _label_ready = True


if __name__ == "__main__":
    main()
