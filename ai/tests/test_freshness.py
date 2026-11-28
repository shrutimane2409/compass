from datetime import date
from pathlib import Path

import httpx
import pytest

from scripts.freshness import (STALE_AFTER_DAYS, Report, audit, check_links, check_rows, classify, fetch_status,
                               latest_ranking_edition, snapshotted_urls, source_urls,
                               upcoming_intake_start)

TODAY = date(2026, 11, 28)
DATA = Path(__file__).resolve().parents[2] / "data"


def course(**kw) -> dict:
    row = {"university_short_name": "UCD", "title": "MSc Test", "fee_academic_year": "2027/28",
           "verified_at": "2026-11-01", "source_url": "https://example.org/c"}
    return row | kw


class TestDates:
    @pytest.mark.parametrize("today,expected", [(date(2026, 11, 28), 2027), (date(2027, 3, 1), 2027), (date(2027, 9, 1), 2028)])
    def test_upcoming_intake(self, today, expected):
        assert upcoming_intake_start(today) == expected

    @pytest.mark.parametrize("source,today,expected", [
        ("QS", date(2026, 5, 1), 2026), ("QS", date(2026, 7, 1), 2027),
        ("THE", date(2026, 9, 1), 2026), ("THE", date(2026, 11, 1), 2027)])
    def test_latest_ranking_edition(self, source, today, expected):
        assert latest_ranking_edition(source, today) == expected


class TestRows:
    def test_fresh_course_with_current_fee_year_is_clean(self):
        r = Report()
        check_rows("courses.csv", [course()], TODAY, r)
        assert r.clean and not r.warnings

    def test_course_older_than_the_window_is_an_error(self):
        r = Report()
        check_rows("courses.csv", [course(verified_at="2026-05-01")], TODAY, r)
        assert len(r.errors) == 1 and "211 days ago" in r.errors[0]

    def test_exactly_at_the_window_is_still_fresh(self):
        r = Report()
        verified = date.fromordinal(TODAY.toordinal() - STALE_AFTER_DAYS).isoformat()
        check_rows("courses.csv", [course(verified_at=verified)], TODAY, r)
        assert r.clean

    @pytest.mark.parametrize("fee_year", ["", "2026/27"])
    def test_missing_or_old_fee_year_is_a_warning_not_an_error(self, fee_year):
        r = Report()
        check_rows("courses.csv", [course(fee_academic_year=fee_year)], TODAY, r)
        assert r.clean and len(r.warnings) == 1

    def test_old_ranking_edition_is_a_warning(self):
        r = Report()
        row = {"university_short_name": "TCD", "source": "QS", "edition": "2026", "verified_at": "2026-11-27",
               "source_url": "https://example.org/r"}
        check_rows("rankings.csv", [row], TODAY, r)
        assert r.clean and any("QS 2027 is out" in w for w in r.warnings)


class TestLinks:
    @pytest.mark.parametrize("status,kind", [(200, "ok"), (301, "ok"), (403, "blocked"), (429, "blocked"),
                                             (404, "broken"), (410, "broken"), (503, "broken"), (None, "broken")])
    def test_classify(self, status, kind):
        assert classify(status) == kind

    def test_server_errors_are_retried_once(self):
        calls = []

        def handler(req):
            calls.append(req)
            return httpx.Response(503 if len(calls) == 1 else 200)

        with httpx.Client(transport=httpx.MockTransport(handler)) as client:
            assert fetch_status("https://example.org", client) == (200, "")
        assert len(calls) == 2

    def test_broken_and_blocked_links_are_reported_separately(self):
        codes = {"https://a.example/ok": 200, "https://a.example/gone": 404, "https://a.example/bot": 403}
        urls = {u: ["UCD MSc Test"] for u in codes}
        r = Report()
        with httpx.Client(transport=httpx.MockTransport(lambda req: httpx.Response(codes[str(req.url)]))) as client:
            check_links(urls, r, client)
        assert len(r.errors) == 1 and "a.example/gone" in r.errors[0]
        assert len(r.warnings) == 1 and "a.example/bot" in r.warnings[0]

    def test_known_bot_blocking_site_is_only_a_warning(self):
        def handler(req):
            raise httpx.ReadTimeout("dropped", request=req)

        r = Report()
        with httpx.Client(transport=httpx.MockTransport(handler)) as client:
            check_links({"https://blocks.example": ["x"]}, r, client, known_blockers={"https://blocks.example"})
        assert r.clean and "open by hand" in r.warnings[0]

    def test_snapshot_lines_in_sources_are_known_blockers(self, tmp_path):
        (tmp_path / "sources.txt").write_text("# comment | not a url\nhttps://a.example\nhttps://b.example | snapshots/b.html\n")
        assert snapshotted_urls(tmp_path) == {"https://b.example"}

    def test_unreachable_host_is_broken(self):
        def handler(req):
            raise httpx.ConnectError("dns", request=req)

        r = Report()
        with httpx.Client(transport=httpx.MockTransport(handler)) as client:
            check_links({"https://nope.example": ["x"]}, r, client)
        assert "ConnectError" in r.errors[0]


class TestRealData:
    def test_every_fact_file_parses_and_names_its_sources(self):
        urls = source_urls(DATA)
        assert len(urls) > 20
        assert all(u.startswith("https://") for u in urls)

    def test_audit_runs_offline_on_the_checked_in_data(self):
        report, checked = audit(DATA, TODAY, links=False)
        assert checked == 0
        assert isinstance(report.markdown(TODAY, checked), str)


def test_report_markdown_lists_fixes_as_tasks():
    r = Report(errors=["UCD MSc Test: last verified 2026-01-01 (331 days ago)"], warnings=["fee year not stated"])
    md = r.markdown(TODAY, 42)
    assert "42 source links checked" in md
    assert "- [ ] UCD MSc Test" in md and "### Check" in md
