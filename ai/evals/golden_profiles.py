"""Golden-profile checks against a running API: properties every shortlist must satisfy.

Usage:  python -m evals.golden_profiles [api_url]     (default http://localhost:5080)

Three layers, all black-box against the live catalogue so they keep working as courses change:
- Per course: every requirement's state is recomputed from the published minimums and the profile, the status
  follows from the states, each requirement cites the page it came from, costs add up, tags are honest.
- Edge profiles: grades on and just below each published minimum, no English test yet, both TOEFL scales,
  a background no course names, a budget too small for anything.
- Metamorphic: a better grade, English score or budget never makes any course worse; the same request gives
  the same answer; scholarships respect their grade minimums and only cover courses the student can apply to.
"""
import sys
import time
from datetime import date, datetime, timezone

import httpx

BORDERLINE_MARGIN = 3.0       # EligibilityEvaluator.BorderlineMarginPercent
NEW_TOEFL_SCALE_MAX = 6.0     # TOEFL iBT scores of 6 or less are on the 1-6 scale
STALE_AFTER_DAYS = 180        # RecommendationEngine.StaleAfterDays
STATUS_RANK = {"NotEligible": 0, "Borderline": 1, "LikelyEligible": 2, "Eligible": 3}
MIN_INTERVAL_S = 1.3          # stay under the API's 60 requests/minute per client

PROFILES = {
    "cs_strong_mid_budget": {"gradePercent": 82, "background": "computer-science",
                             "english": {"test": "Ielts", "score": 7.5}, "workYears": 2,
                             "budgetEur": 45000, "interests": ["machine learning", "AI"]},
    "bba_low_budget": {"gradePercent": 68, "background": "commerce", "english": None, "workYears": 0,
                       "budgetEur": 25000, "interests": ["business analytics"]},
    "it_data_focus": {"gradePercent": 74, "background": "information-technology",
                      "english": {"test": "Pte", "score": 65}, "workYears": 1,
                      "budgetEur": 38000, "interests": ["data science", "cloud"]},
    "arts_low_grade": {"gradePercent": 52, "background": "arts",
                       "english": {"test": "Ielts", "score": 6.5}, "workYears": 0,
                       "budgetEur": 45000, "interests": ["AI", "data science"]},
    # v2 edge profiles
    "cs_no_english_yet": {"gradePercent": 78, "background": "computer-science", "english": None, "workYears": 0,
                          "budgetEur": 50000, "interests": ["software engineering"]},
    "toefl_new_scale": {"gradePercent": 75, "background": "computer-science",
                        "english": {"test": "Toefl", "score": 5.0}, "workYears": 0,
                        "budgetEur": 45000, "interests": ["cybersecurity"]},
    "toefl_old_scale": {"gradePercent": 75, "background": "computer-science",
                        "english": {"test": "Toefl", "score": 100}, "workYears": 0,
                        "budgetEur": 45000, "interests": ["cybersecurity"]},
    "unlisted_background": {"gradePercent": 80, "background": "law",
                            "english": {"test": "Ielts", "score": 7.0}, "workYears": 3,
                            "budgetEur": 60000, "interests": ["AI"]},
    "tiny_budget": {"gradePercent": 70, "background": "computer-science",
                    "english": {"test": "Ielts", "score": 6.5}, "workYears": 0,
                    "budgetEur": 8000, "interests": ["data science"]},
}

# Profiles whose shortlist must contain at least one course they do not qualify for.
EXPECT_INELIGIBLE = {"arts_low_grade", "unlisted_background"}
EXPECT_NOTHING_AFFORDABLE = {"tiny_budget"}


class Api:
    """Thin client that spaces requests out so a full run never trips the rate limiter."""

    def __init__(self, client: httpx.Client):
        self.client, self._last = client, 0.0

    def post(self, path: str, body: dict) -> httpx.Response:
        wait = MIN_INTERVAL_S - (time.monotonic() - self._last)
        if wait > 0:
            time.sleep(wait)
        self._last = time.monotonic()
        return self.client.post(path, json=body)

    def recommend(self, profile: dict) -> dict:
        r = self.post("/api/recommendations", {"profile": profile})
        r.raise_for_status()
        return r.json()

    def scholarships(self, profile: dict, course_ids: list[int] | None = None) -> list[dict]:
        r = self.post("/api/scholarships/match", {"profile": profile, "courseIds": course_ids})
        r.raise_for_status()
        return r.json()


# ---------- expected requirement states, recomputed from the published minimums ----------

def expected_grade(grade: float, course: dict) -> str:
    m = course.get("minGradePercent")
    if m is None:
        return "NotPublished"
    # The API compares decimals; round so float noise (64.6 - 3 = 61.599999...) can't flip a boundary.
    if round(grade - m, 6) >= 0:
        return "Met"
    return "Borderline" if round(grade - (m - BORDERLINE_MARGIN), 6) >= 0 else "NotMet"


def expected_background(background: str, course: dict) -> str:
    accepted = [b.lower() for b in course.get("acceptedBackgrounds") or []]
    if not accepted:
        return "NotPublished"
    if background.lower() in accepted:
        return "Met"
    return "Borderline" if course.get("backgroundsOpen") else "NotMet"


def expected_english(english: dict | None, course: dict) -> str:
    mins = {k: course.get(k) for k in ("minIelts", "minPte", "minToefl", "minToeflNew")}
    if all(v is None for v in mins.values()):
        return "NotPublished"
    if english is None:
        return "StudentPending"
    test, score = english["test"], english["score"]
    key = {"Ielts": "minIelts", "Pte": "minPte",
           "Toefl": "minToeflNew" if score <= NEW_TOEFL_SCALE_MAX else "minToefl"}[test]
    m = mins[key]
    if m is None:
        return "NotPublished"
    return "Met" if score >= m else "NotMet"


def expected_work(years: float, course: dict) -> str:
    m = course.get("minWorkYears") or 0
    return "Met" if m <= 0 or years >= m else "NotMet"


def status_of(states: list[str]) -> str:
    if "NotMet" in states:
        return "NotEligible"
    if "Borderline" in states:
        return "Borderline"
    if any(s in ("NotPublished", "StudentPending") for s in states):
        return "LikelyEligible"
    return "Eligible"


def expected_sources(course: dict) -> dict[str, str]:
    """Each requirement must cite the page it actually comes from, not just the course page."""
    entry = course.get("entrySourceUrl") or course["sourceUrl"]
    return {"Grade": course.get("gradeSourceUrl") or entry, "Background": entry, "Work experience": entry,
            "English": course.get("englishSourceUrl") or course["sourceUrl"]}


def academic_start(label: str | None) -> int | None:
    return int(label[:4]) if label and len(label) >= 4 and label[:4].isdigit() else None


# ---------- invariants ----------

def item_failures(p: dict, item: dict, target_intake: str, today: date) -> list[str]:
    c, cost, elig = item["course"], item["cost"], item["eligibility"]
    title, failures = c["title"], []
    checks = {ch["name"]: ch for ch in elig["checks"]}

    expected = {
        "Grade": expected_grade(p["gradePercent"], c),
        "Background": expected_background(p["background"], c),
        "English": expected_english(p["english"], c),
        "Work experience": expected_work(p["workYears"], c),
    }
    for name, want in expected.items():
        got = checks.get(name, {}).get("state")
        if got != want:
            failures.append(f"{title}: {name} is {got}, rules give {want}")
    if elig["status"] != status_of([ch["state"] for ch in elig["checks"]]):
        failures.append(f"{title}: status {elig['status']} doesn't follow from its checks")
    if elig["status"] == "Eligible" and any(ch["state"] != "Met" for ch in elig["checks"]):
        failures.append(f"{title}: shown as Eligible with an unmet or unpublished requirement")

    for name, url in expected_sources(c).items():
        if name in checks and checks[name].get("sourceUrl") != url:
            failures.append(f"{title}: {name} cites {checks[name].get('sourceUrl')}, expected {url}")
    if any(not (ch.get("sourceUrl") or "").startswith("https://") for ch in elig["checks"]):
        failures.append(f"{title}: a requirement has no source link")
    if not c["sourceUrl"].startswith("https://"):
        failures.append(f"{title}: missing official source")

    if abs(cost["tuition"] + cost["living"] + cost["oneOff"] - cost["total"]) > 0.01:
        failures.append(f"{title}: cost parts do not add up to the total")
    if item["affordability"] == "Affordable" and cost["total"] > p["budgetEur"]:
        failures.append(f"{title}: tagged affordable above budget")
    if not item["reasons"]:
        failures.append(f"{title}: no explanation")

    tags = set(item["tags"])
    fee_year, intake = academic_start(c.get("feeAcademicYear")), academic_start(target_intake)
    if ("FeeYearUnconfirmed" in tags) != (fee_year is None or fee_year < intake):
        failures.append(f"{title}: FeeYearUnconfirmed tag wrong for fee year {c.get('feeAcademicYear')!r}")
    age = (today - date.fromisoformat(c["verifiedAt"][:10])).days
    if abs(age - STALE_AFTER_DAYS) > 1 and ("StaleData" in tags) != (age > STALE_AFTER_DAYS):
        failures.append(f"{title}: StaleData tag wrong for data verified {age} days ago")
    return failures


def shortlist_failures(name: str, p: dict, res: dict, today: date) -> list[str]:
    items, failures = res["items"], []
    statuses = [i["eligibility"]["status"] for i in items]
    first_ineligible = next((n for n, s in enumerate(statuses) if s == "NotEligible"), len(statuses))
    if any(s != "NotEligible" for s in statuses[first_ineligible:]):
        failures.append("an ineligible course is ranked above an eligible one")
    if name in EXPECT_INELIGIBLE and "NotEligible" not in statuses:
        failures.append("expected at least one ineligible course")
    if name in EXPECT_NOTHING_AFFORDABLE and any(i["affordability"] == "Affordable" for i in items):
        failures.append("a course is affordable on a budget below any course's fee")
    for i in items:
        failures += item_failures(p, i, res["targetIntake"], today)
    return failures


def by_course(res: dict) -> dict[int, dict]:
    return {i["course"]["id"]: i for i in res["items"]}


def never_worse(before: dict, after: dict, change: str) -> list[str]:
    """After improving one input, no course's status may drop and no affordable course may stop being affordable."""
    failures, a = [], by_course(after)
    for cid, b in by_course(before).items():
        if cid not in a:
            failures.append(f"{change}: course {cid} disappeared")
            continue
        sb, sa = b["eligibility"]["status"], a[cid]["eligibility"]["status"]
        if STATUS_RANK[sa] < STATUS_RANK[sb]:
            failures.append(f"{change}: {b['course']['title']} went from {sb} to {sa}")
        if b["affordability"] == "Affordable" and a[cid]["affordability"] != "Affordable":
            failures.append(f"{change}: {b['course']['title']} stopped being affordable")
    return failures


def same_decisions(a: dict, b: dict) -> list[str]:
    """Eligibility, cost and tags are rules only, so two identical requests must agree exactly."""
    key = lambda r: {i["course"]["id"]: (i["eligibility"], i["cost"], i["affordability"], sorted(i["tags"]))
                     for i in r["items"]}
    return [] if key(a) == key(b) else ["the same profile gave different eligibility, cost or tags"]


def grade_boundary_profiles(base: dict, courses: list[dict]) -> list[dict]:
    """For each published minimum m: exactly m (Met), m - margin (Borderline), just below that (NotMet)."""
    grades = set()
    for m in {c["minGradePercent"] for c in courses if c.get("minGradePercent") is not None}:
        grades |= {round(m, 1), round(m - BORDERLINE_MARGIN, 1), round(m - BORDERLINE_MARGIN - 0.5, 1)}
    return [{**base, "gradePercent": g} for g in sorted(grades) if 0 <= g <= 100]


def scholarship_failures(p: dict, matches: list[dict], allowed_course_ids: set[int]) -> list[str]:
    failures = []
    for m in matches:
        s, fit, ids = m["scholarship"], m["fit"], set(m["courseIds"])
        name = s.get("slug") or s["name"]
        if not s["sourceUrl"].startswith("https://"):
            failures.append(f"scholarship {name}: missing official source")
        if not ids <= allowed_course_ids:
            failures.append(f"scholarship {name}: covers courses the student isn't considering {sorted(ids - allowed_course_ids)}")
        if not ids and fit != "NotApplicable":
            failures.append(f"scholarship {name}: {fit} with no matching course")
        minimum = s.get("minGradePercent")
        if minimum is not None and p["gradePercent"] < minimum and fit != "NotApplicable":
            failures.append(f"scholarship {name}: {fit} although the grade is below its {minimum}% minimum")
        if fit == "Likely" and not (s["automatic"] and not s["competitive"]):
            failures.append(f"scholarship {name}: Likely but it is competitive or needs a separate application")
        if not m["reasons"]:
            failures.append(f"scholarship {name}: no explanation")
    return failures


def scholarship_boundary_failures(api: Api, base: dict) -> list[str]:
    """At a scholarship's grade minimum the student is in scope; half a point below, never."""
    failures = []
    all_matches = api.scholarships({**base, "gradePercent": 100})
    for m in all_matches:
        minimum, name = m["scholarship"].get("minGradePercent"), m["scholarship"].get("slug") or m["scholarship"]["name"]
        if minimum is None or not m["courseIds"]:
            continue
        below = next(x for x in api.scholarships({**base, "gradePercent": minimum - 0.5})
                     if x["scholarship"]["id"] == m["scholarship"]["id"])
        if below["fit"] != "NotApplicable":
            failures.append(f"scholarship {name}: {below['fit']} at {minimum - 0.5}%, below its {minimum}% minimum")
        at = next(x for x in api.scholarships({**base, "gradePercent": minimum, "budgetEur": base["budgetEur"]})
                  if x["scholarship"]["id"] == m["scholarship"]["id"])
        if at["courseIds"] and at["fit"] == "NotApplicable":
            failures.append(f"scholarship {name}: NotApplicable at exactly its {minimum}% minimum")
    return failures


# ---------- runner ----------

def run(api: Api, today: date) -> dict[str, list[str]]:
    results: dict[str, list[str]] = {}
    shortlists: dict[str, dict] = {}

    for name, p in PROFILES.items():
        res = api.recommend(p)
        shortlists[name] = res
        problems = shortlist_failures(name, p, res, today)
        problems += never_worse(res, api.recommend({**p, "budgetEur": p["budgetEur"] + 10000}), "budget +10000")
        if p["gradePercent"] <= 95:
            problems += never_worse(res, api.recommend({**p, "gradePercent": p["gradePercent"] + 5}), "grade +5")
        eligible_ids = {i["course"]["id"] for i in res["items"] if i["eligibility"]["status"] != "NotEligible"}
        problems += scholarship_failures(p, api.scholarships(p), eligible_ids)
        results[name] = problems

    base = PROFILES["cs_strong_mid_budget"]
    courses = [i["course"] for i in shortlists["cs_strong_mid_budget"]["items"]]

    boundary = []
    for p in grade_boundary_profiles(base, courses):
        boundary += [f"grade {p['gradePercent']}%: {f}" for f in shortlist_failures("boundary", p, api.recommend(p), today)]
    results["grade_boundaries"] = boundary

    results["english_improves"] = never_worse(
        shortlists["cs_no_english_yet"],
        api.recommend({**PROFILES["cs_no_english_yet"], "english": {"test": "Ielts", "score": 9.0}}),
        "IELTS 9.0 added")
    results["deterministic"] = same_decisions(shortlists["cs_strong_mid_budget"], api.recommend(base))

    one = courses[0]["id"]
    saved = api.scholarships(base, [one])
    results["scholarships_saved_course"] = scholarship_failures(base, saved, {one})
    results["scholarship_boundaries"] = scholarship_boundary_failures(api, base)

    invalid = []
    for field, value in (("gradePercent", 101), ("gradePercent", -1), ("budgetEur", 0), ("workYears", 60)):
        r = api.post("/api/recommendations", {"profile": {**base, field: value}})
        if r.status_code != 400:
            invalid.append(f"{field}={value} returned {r.status_code}, expected 400")
    results["rejects_invalid_profiles"] = invalid
    return results


def main(url: str) -> None:
    today = datetime.now(timezone.utc).date()
    with httpx.Client(base_url=url, timeout=30) as client:
        results = run(Api(client), today)
    failed = False
    for name, problems in results.items():
        failed |= bool(problems)
        print(f"{'PASS' if not problems else 'FAIL'} {name}")
        for msg in problems[:20]:
            print(f"     - {msg}")
        if len(problems) > 20:
            print(f"     ... and {len(problems) - 20} more")
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5080")
