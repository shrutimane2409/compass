"""The golden-profile checker itself: each invariant must pass a correct answer and catch a wrong one."""
import copy
from datetime import date

import pytest

from evals.golden_profiles import (PROFILES, expected_background, expected_english, expected_grade, expected_sources,
                                   expected_work, grade_boundary_profiles, item_failures, never_worse, same_decisions,
                                   scholarship_failures, shortlist_failures, status_of)

TODAY = date(2026, 11, 28)
P = PROFILES["cs_strong_mid_budget"]  # 82%, computer-science, IELTS 7.5, 2 years, €45k


def course(**kw) -> dict:
    c = {"id": 1, "title": "MSc Test", "minGradePercent": 60.0, "acceptedBackgrounds": ["computer-science"],
         "backgroundsOpen": False, "minIelts": 6.5, "minPte": 63, "minToefl": 90, "minToeflNew": None,
         "minWorkYears": 0, "sourceUrl": "https://u.example/course", "entrySourceUrl": "https://u.example/entry",
         "gradeSourceUrl": "https://u.example/india", "englishSourceUrl": "https://u.example/english",
         "feeAcademicYear": "2027/28", "verifiedAt": "2026-11-19"}
    return c | kw


def item(c: dict | None = None, profile: dict = P, **kw) -> dict:
    """A correct API item for this course and profile, built with the same rules the API uses."""
    c = c or course()
    states = {"Grade": expected_grade(profile["gradePercent"], c),
              "Background": expected_background(profile["background"], c),
              "English": expected_english(profile["english"], c),
              "Work experience": expected_work(profile["workYears"], c)}
    sources = expected_sources(c)
    checks = [{"name": n, "state": s, "sourceUrl": sources[n]} for n, s in states.items()]
    it = {"course": c, "eligibility": {"status": status_of(list(states.values())), "checks": checks},
          "cost": {"tuition": 20000, "living": 15000, "oneOff": 1000, "total": 36000},
          "affordability": "Affordable", "tags": [], "reasons": ["why"]}
    return it | kw


def check(it: dict, name: str) -> dict:
    return next(ch for ch in it["eligibility"]["checks"] if ch["name"] == name)


class TestRules:
    @pytest.mark.parametrize("grade,state", [(60, "Met"), (59.5, "Borderline"), (57, "Borderline"), (56.9, "NotMet")])
    def test_grade_margin(self, grade, state):
        assert expected_grade(grade, course()) == state

    def test_unpublished_grade(self):
        assert expected_grade(90, course(minGradePercent=None)) == "NotPublished"

    @pytest.mark.parametrize("bg,open_,state", [("computer-science", False, "Met"), ("Computer-Science", False, "Met"),
                                                ("law", False, "NotMet"), ("law", True, "Borderline")])
    def test_background(self, bg, open_, state):
        assert expected_background(bg, course(backgroundsOpen=open_)) == state

    def test_background_not_published(self):
        assert expected_background("law", course(acceptedBackgrounds=[])) == "NotPublished"

    @pytest.mark.parametrize("english,state", [
        (None, "StudentPending"),
        ({"test": "Ielts", "score": 6.5}, "Met"), ({"test": "Ielts", "score": 6.0}, "NotMet"),
        ({"test": "Toefl", "score": 95}, "Met"),
        ({"test": "Toefl", "score": 5.0}, "NotPublished"),  # new 1-6 scale, course publishes only the old one
    ])
    def test_english(self, english, state):
        assert expected_english(english, course()) == state

    def test_english_new_toefl_scale_uses_its_own_minimum(self):
        c = course(minToeflNew=4.5)
        assert expected_english({"test": "Toefl", "score": 5.0}, c) == "Met"
        assert expected_english({"test": "Toefl", "score": 4.0}, c) == "NotMet"

    def test_english_nothing_published(self):
        c = course(minIelts=None, minPte=None, minToefl=None, minToeflNew=None)
        assert expected_english(None, c) == "NotPublished"

    @pytest.mark.parametrize("states,status", [
        (["Met"] * 4, "Eligible"), (["Met", "StudentPending", "Met", "Met"], "LikelyEligible"),
        (["Borderline", "NotPublished", "Met", "Met"], "Borderline"), (["Borderline", "NotMet", "Met", "Met"], "NotEligible")])
    def test_status_precedence(self, states, status):
        assert status_of(states) == status

    def test_sources_fall_back_to_the_course_page(self):
        c = course(entrySourceUrl=None, gradeSourceUrl=None, englishSourceUrl=None)
        assert set(expected_sources(c).values()) == {"https://u.example/course"}


class TestItemInvariants:
    def test_correct_item_passes(self):
        assert item_failures(P, item(), "2027/28", TODAY) == []

    def test_wrong_state_is_caught(self):
        it = item(course(minGradePercent=85.0))  # 82% is Borderline, not Met
        check(it, "Grade")["state"] = "Met"
        assert any("Grade is Met, rules give Borderline" in f for f in item_failures(P, it, "2027/28", TODAY))

    def test_green_status_with_a_pending_requirement_is_caught(self):
        p = PROFILES["cs_no_english_yet"]
        it = item(profile=p)
        it["eligibility"]["status"] = "Eligible"
        failures = item_failures(p, it, "2027/28", TODAY)
        assert any("doesn't follow from its checks" in f for f in failures)
        assert any("shown as Eligible" in f for f in failures)

    def test_requirement_citing_the_wrong_page_is_caught(self):
        it = item()
        check(it, "English")["sourceUrl"] = "https://u.example/course"
        assert any("English cites" in f for f in item_failures(P, it, "2027/28", TODAY))

    def test_cost_and_affordability(self):
        it = item(cost={"tuition": 20000, "living": 15000, "oneOff": 1000, "total": 50000})
        failures = item_failures(P, it, "2027/28", TODAY)
        assert any("do not add up" in f for f in failures)
        assert any("affordable above budget" in f for f in failures)

    @pytest.mark.parametrize("fee_year,tagged", [("2027/28", False), ("2026/27", True), (None, True)])
    def test_fee_year_tag_must_match(self, fee_year, tagged):
        right = item(course(feeAcademicYear=fee_year), tags=["FeeYearUnconfirmed"] if tagged else [])
        wrong = item(course(feeAcademicYear=fee_year), tags=[] if tagged else ["FeeYearUnconfirmed"])
        assert item_failures(P, right, "2027/28", TODAY) == []
        assert any("FeeYearUnconfirmed" in f for f in item_failures(P, wrong, "2027/28", TODAY))

    def test_stale_tag_must_match(self):
        old = course(verifiedAt="2026-01-01")
        assert item_failures(P, item(old, tags=["StaleData"]), "2027/28", TODAY) == []
        assert any("StaleData" in f for f in item_failures(P, item(old), "2027/28", TODAY))


class TestShortlist:
    def test_ineligible_above_eligible_is_caught(self):
        bad = item(course(id=2, minGradePercent=95.0))
        res = {"items": [bad, item()], "targetIntake": "2027/28"}
        assert "an ineligible course is ranked above an eligible one" in shortlist_failures("x", P, res, TODAY)

    def test_tiny_budget_with_nothing_affordable_passes(self):
        p = PROFILES["tiny_budget"]
        res = {"items": [item(profile=p, affordability="OverBudget")], "targetIntake": "2027/28"}
        assert shortlist_failures("tiny_budget", p, res, TODAY) == []


class TestMetamorphic:
    def test_never_worse(self):
        before = {"items": [item()]}
        after = copy.deepcopy(before)
        assert never_worse(before, after, "grade +5") == []
        after["items"][0]["eligibility"]["status"] = "NotEligible"
        after["items"][0]["affordability"] = "Stretch"
        failures = never_worse(before, after, "grade +5")
        assert len(failures) == 2

    def test_same_decisions(self):
        a = {"items": [item()]}
        b = copy.deepcopy(a)
        assert same_decisions(a, b) == []
        b["items"][0]["cost"]["total"] += 1
        assert same_decisions(a, b)

    def test_boundary_grades_are_exact_decimals(self):
        grades = [p["gradePercent"] for p in grade_boundary_profiles(P, [course(minGradePercent=64.6)])]
        assert grades == [61.1, 61.6, 64.6]
        assert expected_grade(64.6 - 3, course(minGradePercent=64.6)) == "Borderline"

    def test_boundary_profiles_straddle_each_minimum(self):
        grades = [p["gradePercent"] for p in grade_boundary_profiles(P, [course(), course(id=2, minGradePercent=None)])]
        assert grades == [56.5, 57.0, 60.0]
        assert [expected_grade(g, course()) for g in grades] == ["NotMet", "Borderline", "Met"]


class TestScholarships:
    def match(self, **kw) -> dict:
        s = {"id": 1, "slug": "s", "name": "S", "minGradePercent": None, "automatic": False, "competitive": True,
             "sourceUrl": "https://s.example"}
        m = {"scholarship": s, "fit": "Possible", "courseIds": [1], "reasons": ["why"]}
        for k, v in kw.items():
            (s if k in s else m)[k] = v
        return m

    def test_valid_match_passes(self):
        assert scholarship_failures(P, [self.match()], {1}) == []

    def test_below_grade_minimum_must_be_not_applicable(self):
        assert any("below its" in f for f in scholarship_failures(P, [self.match(minGradePercent=85)], {1}))
        assert scholarship_failures(P, [self.match(minGradePercent=85, fit="NotApplicable")], {1}) == []

    def test_courses_outside_the_list_are_caught(self):
        assert any("isn't considering" in f for f in scholarship_failures(P, [self.match(courseIds=[2])], {1}))

    def test_likely_needs_automatic_and_non_competitive(self):
        assert any("Likely but" in f for f in scholarship_failures(P, [self.match(fit="Likely")], {1}))
        assert scholarship_failures(P, [self.match(fit="Likely", automatic=True, competitive=False)], {1}) == []

    def test_fit_without_courses_is_caught(self):
        assert any("no matching course" in f for f in scholarship_failures(P, [self.match(courseIds=[])], {1}))
