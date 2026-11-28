"""Daily data-freshness audit over the checked-in data files (the source of truth loaded into the database).

Usage (from ai/):  python -m scripts.freshness [--data ../data] [--today YYYY-MM-DD] [--no-links] [--issue]

Checks
- Every fact row was verified within STALE_AFTER_DAYS (the same window the API uses for its "Check dates" tag).
- Course fees name an academic year, and it is not older than the upcoming intake.
- Ranking editions are the latest one published (an older edition is kept when the newer one omits the university).
- Every source URL behind a fact still loads. Sites that block automated clients (401/403/429) are
  reported as "blocked", not broken: check those by hand.

Errors (stale facts, broken links) make the audit fail. Warnings (fee year behind the upcoming intake, older
ranking editions, blocked sites) are reported only, because the app already flags them or they need judgement.
With --issue the report goes to one GitHub issue (label data-freshness), which is closed again once clean.
"""
import argparse
import csv
import subprocess
import sys
from collections.abc import Iterable
from dataclasses import dataclass, field
from datetime import date
from pathlib import Path

import httpx

STALE_AFTER_DAYS = 180  # keep in step with RecommendationEngine.StaleAfterDays
USER_AGENT = "CompassBot/0.1 (student project; source link check)"
ISSUE_TITLE = "Data freshness report"
ISSUE_LABEL = "data-freshness"
BLOCKED_CODES = {401, 403, 429, 999}

# file -> (columns holding source URLs, how to name a row in the report)
FILES = {
    "courses.csv": (("source_url", "entry_source_url", "grade_source_url", "english_source_url"),
                    lambda r: f"{r['university_short_name']} {r['title']}"),
    "living_costs.csv": (("source_url",), lambda r: f"living cost {r['city']} {r['category']}"),
    "one_off_costs.csv": (("source_url",), lambda r: f"one-off cost {r['code']}"),
    "rankings.csv": (("source_url",), lambda r: f"{r['source']} {r['edition']} {r['university_short_name']}"),
    "scholarships.csv": (("source_url",), lambda r: f"scholarship {r['slug']}"),
}


@dataclass
class Report:
    errors: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)

    @property
    def clean(self) -> bool:
        return not self.errors

    def markdown(self, today: date, checked_links: int) -> str:
        lines = [f"Audit of `data/` on {today.isoformat()} ({checked_links} source links checked).", ""]
        if self.clean and not self.warnings:
            return "\n".join(lines + ["All facts are fresh and every source link loads."])
        if self.errors:
            lines += ["### Fix", ""] + [f"- [ ] {e}" for e in self.errors] + [""]
        if self.warnings:
            lines += ["### Check", ""] + [f"- {w}" for w in self.warnings] + [""]
        lines.append("Re-verify against the official page, update `data/*.csv` (value and `verified_at`), "
                     "then reload with the matching `scripts.load_*`.")
        return "\n".join(lines)


def upcoming_intake_start(today: date) -> int:
    """First year of the next September intake: November 2026 -> 2027 (the 2027/28 intake)."""
    return today.year + 1 if today.month >= 9 else today.year


def latest_ranking_edition(source: str, today: date) -> int:
    """QS publishes edition N+1 in June of year N; THE publishes in early October."""
    published_by = 6 if source.upper() == "QS" else 10
    return today.year + 1 if today.month > published_by else today.year


def academic_start(label: str | None) -> int | None:
    label = (label or "").strip()
    return int(label[:4]) if len(label) >= 4 and label[:4].isdigit() else None


def check_rows(name: str, rows: Iterable[dict], today: date, report: Report) -> None:
    describe = FILES[name][1]
    intake = upcoming_intake_start(today)
    for r in rows:
        what = describe(r)
        verified = date.fromisoformat(r["verified_at"].strip())
        age = (today - verified).days
        if age > STALE_AFTER_DAYS:
            report.errors.append(f"{what}: last verified {verified} ({age} days ago)")
        if name == "courses.csv":
            fee_year = academic_start(r.get("fee_academic_year"))
            if fee_year is None:
                report.warnings.append(f"{what}: fee year not stated on the page")
            elif fee_year < intake:
                report.warnings.append(f"{what}: fee is for {r['fee_academic_year']}, upcoming intake is "
                                       f"{intake}/{(intake + 1) % 100:02d}")
        if name == "rankings.csv":
            latest = latest_ranking_edition(r["source"], today)
            if int(r["edition"]) < latest:
                # Kept on purpose when the newer edition doesn't list the university, so only flag it.
                report.warnings.append(f"{what}: {r['source']} {latest} is out; update unless it doesn't list this university")


def source_urls(data_dir: Path) -> dict[str, list[str]]:
    """Each distinct source URL -> the facts that cite it."""
    urls: dict[str, list[str]] = {}
    for name, (columns, describe) in FILES.items():
        for r in read(data_dir / name):
            for col in columns:
                url = (r.get(col) or "").strip()
                if url:
                    urls.setdefault(url, []).append(describe(r))
    return urls


def classify(status: int | None) -> str:
    """ok, blocked (site refuses bots: check by hand) or broken. None means no response at all."""
    if status is None:
        return "broken"
    if status < 400:
        return "ok"
    return "blocked" if status in BLOCKED_CODES else "broken"


def fetch_status(url: str, client: httpx.Client) -> tuple[int | None, str]:
    """Status of a GET, retried once on network errors and 5xx (free-tier and university servers blip)."""
    detail = ""
    for _ in range(2):
        try:
            res = client.get(url)
            if res.status_code < 500:
                return res.status_code, ""
            detail = f"HTTP {res.status_code}"
        except httpx.HTTPError as ex:
            detail = type(ex).__name__
    return (int(detail[5:]) if detail.startswith("HTTP ") else None), detail


def snapshotted_urls(data_dir: Path) -> set[str]:
    """Pages known to block automated clients: sources.txt gives them a saved copy ('<url> | snapshots/...')."""
    path = data_dir / "sources.txt"
    if not path.exists():
        return set()
    lines = (line.strip() for line in path.read_text(encoding="utf-8").splitlines())
    return {line.split("|")[0].strip() for line in lines if "|" in line and not line.startswith("#")}


def check_links(urls: dict[str, list[str]], report: Report, client: httpx.Client,
                known_blockers: set[str] = frozenset()) -> None:
    for url, facts in sorted(urls.items()):
        status, detail = fetch_status(url, client)
        kind = classify(status)
        if kind == "broken" and url in known_blockers:
            report.warnings.append(f"known bot-blocking site did not answer ({detail or f'HTTP {status}'}), "
                                   f"open by hand: {url}")
            continue
        cited_by = f"{len(facts)} fact(s), e.g. {facts[0]}"
        if kind == "broken":
            report.errors.append(f"broken link ({detail or f'HTTP {status}'}): {url} - cited by {cited_by}")
        elif kind == "blocked":
            report.warnings.append(f"site blocks automated checks (HTTP {status}), open by hand: {url}")


def read(path: Path) -> list[dict]:
    with path.open(encoding="utf-8") as f:
        return list(csv.DictReader(f))


def audit(data_dir: Path, today: date, links: bool) -> tuple[Report, int]:
    report = Report()
    for name in FILES:
        check_rows(name, read(data_dir / name), today, report)
    urls = source_urls(data_dir)
    if links:
        headers = {"User-Agent": USER_AGENT, "Accept": "text/html,application/xhtml+xml"}
        with httpx.Client(headers=headers, timeout=20, follow_redirects=True) as client:
            check_links(urls, report, client, snapshotted_urls(data_dir))
    return report, len(urls) if links else 0


def gh(*args: str) -> str:
    return subprocess.run(["gh", *args], check=True, capture_output=True, text=True).stdout.strip()


def publish(report: Report, body: str) -> None:
    """One open issue at most: created or updated while there is something to fix, closed once clean."""
    subprocess.run(["gh", "label", "create", ISSUE_LABEL, "--color", "fbca04", "--force"], check=False, capture_output=True)
    existing = gh("issue", "list", "--label", ISSUE_LABEL, "--state", "open", "--json", "number", "--jq", ".[0].number")
    if report.clean:
        if existing:
            gh("issue", "close", existing, "--comment", body)
        return
    if existing:
        gh("issue", "edit", existing, "--body", body)
    else:
        gh("issue", "create", "--title", ISSUE_TITLE, "--label", ISSUE_LABEL, "--body", body)


def main() -> None:
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument("--data", default="../data", type=Path)
    p.add_argument("--today", type=date.fromisoformat, default=date.today())
    p.add_argument("--no-links", action="store_true", help="skip the network check")
    p.add_argument("--issue", action="store_true", help="report to a GitHub issue instead of failing")
    args = p.parse_args()

    report, checked = audit(args.data, args.today, links=not args.no_links)
    body = report.markdown(args.today, checked)
    print(body)
    if args.issue:
        publish(report, body)
    elif not report.clean:
        sys.exit(1)


if __name__ == "__main__":
    main()
