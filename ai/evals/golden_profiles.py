"""Golden-profile checks against a running API: properties every shortlist must satisfy.

Usage:  python -m evals.golden_profiles [api_url]     (default http://localhost:5080)
"""
import sys

import httpx

PROFILES = {
    "cs_strong_mid_budget": {"gradePercent": 82, "background": "computer-science",
                             "english": {"test": "Ielts", "score": 7.5}, "workYears": 2,
                             "budgetEur": 45000, "interests": ["machine learning", "AI"]},
    "bba_low_budget": {"gradePercent": 68, "background": "commerce", "english": None, "workYears": 0,
                       "budgetEur": 25000, "interests": ["business analytics"]},
    "it_data_focus": {"gradePercent": 74, "background": "information-technology",
                      "english": {"test": "Pte", "score": 65}, "workYears": 1,
                      "budgetEur": 38000, "interests": ["data science", "cloud"]},
}


def recommend(client: httpx.Client, profile: dict) -> dict:
    r = client.post("/api/recommendations", json={"profile": profile})
    r.raise_for_status()
    return r.json()


def check(name: str, client: httpx.Client) -> list[str]:
    p = PROFILES[name]
    res = recommend(client, p)
    items, failures = res["items"], []

    statuses = [i["eligibility"]["status"] for i in items]
    first_ineligible = next((n for n, s in enumerate(statuses) if s == "NotEligible"), len(statuses))
    if any(s != "NotEligible" for s in statuses[first_ineligible:]):
        failures.append("an ineligible course is ranked above an eligible one")

    for i in items:
        c, cost = i["course"], i["cost"]
        if not c["sourceUrl"].startswith("https://"):
            failures.append(f"{c['title']}: missing official source")
        if abs(cost["tuition"] + cost["living"] + cost["oneOff"] - cost["total"]) > 0.01:
            failures.append(f"{c['title']}: cost parts do not add up to the total")
        if not i["reasons"]:
            failures.append(f"{c['title']}: no explanation")
        afford = i["affordability"]
        if afford == "Affordable" and cost["total"] > p["budgetEur"]:
            failures.append(f"{c['title']}: tagged affordable above budget")

    # Raising the budget must never reduce the number of affordable courses
    richer = recommend(client, {**p, "budgetEur": p["budgetEur"] + 10000})
    count = lambda r: sum(i["affordability"] == "Affordable" for i in r["items"])
    if count(richer) < count(res):
        failures.append("raising the budget reduced affordable courses")

    return failures


def main(api: str) -> None:
    failed = False
    with httpx.Client(base_url=api, timeout=15) as client:
        for name in PROFILES:
            problems = check(name, client)
            failed |= bool(problems)
            print(f"{'PASS' if not problems else 'FAIL'} {name}")
            for msg in problems:
                print(f"     - {msg}")
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5080")
