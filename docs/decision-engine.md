# Compass · Decision engine

How Compass turns a student profile into a shortlist, and what it does to avoid giving a student false confidence.
Students act on these results (they apply, they pay deposits), so every rule below prefers **"we don't know"** over a confident wrong answer.

## 1. Principles

1. **Facts are decided by code, not by a model.** Eligibility, cost, affordability and freshness are deterministic, unit-tested C#.
   The LLM never decides whether a student qualifies or what a course costs.
2. **Missing information is never green.** A requirement the university doesn't publish is shown as *not published*, not as met.
3. **Every fact has a source.** Each requirement links to the official page it came from; each course shows when it was last verified.
4. **Uncertainty is visible.** Borderline grades, fees from earlier academic years, derived values and stale data are tagged.
5. **The student controls the trade-off.** Ranking weights are explicit and the student can change what matters most.
6. **Errors flow back.** Students can report wrong information; reports are reviewed against the official page before data changes.

## 2. Data provenance

| Fact | Source | Where it is recorded |
|---|---|---|
| Fee, duration, intake | Course page or fee schedule | `courses.source_url`, `fee_academic_year` |
| Grade minimum for Indian degrees | University's India entry table | `grade_source_url` |
| Accepted backgrounds | Course entry requirements | `entry_source_url` |
| English minimums | Course page or university-wide English policy | `english_source_url` |
| Living costs | University cost-of-living pages | `living_costs.source_url`, `is_estimate` |
| One-off costs | University pre-arrival cost tables | `one_off_costs.source_url` |
| EUR → INR | European Central Bank daily reference rate | `/api/fx/INR` (date and source returned) |

Rules: official pages only; `verified_at` is the date a human checked the page; `is_estimate` marks any derived value
(range midpoint, unstated duration, older-year fee); a field the page doesn't state is left empty.

## 3. Eligibility

Each requirement produces a check with one of five states:

| State | Meaning |
|---|---|
| Met | Published and the student meets it |
| NotMet | Published and the student doesn't meet it |
| Borderline | Within 3 percentage points below a published grade minimum, or a "related disciplines" case the university decides |
| NotPublished | The university doesn't publish this requirement for the course |
| StudentPending | Published, but the student hasn't supplied the value yet (e.g. no English test taken) |

The course status follows strict precedence:

    any NotMet          → NotEligible
    else any Borderline → Borderline       (shown as "Reach")
    else any unknown    → LikelyEligible   (shown as "Check requirements")
    else                → Eligible         (every requirement published and met)

English: IELTS, PTE and TOEFL are compared against their own minimums, never converted between tests.
TOEFL iBT changed to a 1–6 band scale on 21 January 2026; a score of 6 or below is compared with the new-scale minimum.

## 4. Cost

    total = tuition (programme fee, non-EU) + monthly living × duration + one-off costs

- Tuition shows its academic year. If the fee is for an earlier year than the upcoming intake, or the year isn't stated,
  the course is tagged **Fee year unconfirmed**: fees usually rise year on year.
- Affordability: total ≤ budget → Affordable; ≤ budget + 10% → Stretch; otherwise Over budget.
- The rupee budget converts at the ECB reference rate for the day (editable by the student, marked "edited").

## 5. Course fit

Fit is semantic similarity between the student's interests and the course description (Gemini embeddings, pgvector),
mapped onto 0–1. It only reorders courses. If fit scoring is unavailable, every course gets a neutral 0.5,
no course is tagged *Good fit*, and the UI says ranking is on eligibility and cost only.

## 6. Ranking

    score = w_fit × fit + w_afford × affordability + w_elig × eligibility

| Component | Value |
|---|---|
| fit | 0–1 from §5 |
| affordability | Affordable: 0.7–1.0 by budget headroom (full score at 25% left over) · Stretch: 0.35 · Over budget: 0 |
| eligibility | Eligible 1.0 · LikelyEligible 0.75 · Borderline 0.4 · NotEligible 0 |

| Priority | w_fit | w_afford | w_elig |
|---|---|---|---|
| Balanced (default) | 0.50 | 0.30 | 0.20 |
| Best course fit | 0.70 | 0.15 | 0.15 |
| Lowest cost | 0.25 | 0.60 | 0.15 |

Not-eligible courses always sort last. Ties break by lower total cost, then course id, so results are deterministic.
Every result returns its score breakdown, and the UI shows it under "Why this result".

## 7. Grounded answers (chat)

- The agent may state eligibility and cost only from the rules engine (`get_recommendations` tool), and
  visa, scholarship or course details only from retrieved official pages (`search_sources` tool).
- After each answer, every cited link is checked against the `sourceUrl`s the tools actually returned that turn.
  The UI shows **✓ based on N official sources**, **⚠ links not among retrieved sources**, or
  **general answer, not checked** when no tool was used.
- Retrieved text is treated as data; instructions inside it are ignored. Tools are read-only.

## 8. Feedback and correction loop

1. Student clicks **Report an error** on a course (field + message) → `course_feedback` (status `open`).
2. A maintainer checks the official page.
3. If confirmed: update `data/courses.csv` with the new value, source and `verified_at`, reload, mark the report `fixed`.
4. The weekly source refresh also re-fetches every official page and lists pages whose content changed, so courses
   sourced from them are re-verified even without a report.

## 9. How correctness is checked

| Check | What it guarantees |
|---|---|
| xUnit rule tests | Every eligibility state and precedence, provenance links, fee-year flag, ranking weights, tie-breaks |
| Golden profiles against the live API | Ineligible never above eligible; never "Eligible" with an unmet or unpublished requirement; every requirement sourced; costs add up; more budget never means fewer affordable courses |
| Retrieval evaluation | The right official page is retrieved for real student questions (hit@6 = 1.00, MRR = 0.84 on 12 questions) |
| Citation tests | Links not returned by tools are flagged; answers without tools are never marked grounded |

## 10. Known limits

- Alternative entry routes (work experience instead of a grade, RPL) and per-band English minimums are not modelled yet;
  affected courses show *Check requirements* rather than a false *Not eligible*.
- Living costs come from each university's own estimate, so methods differ slightly between cities; derived values are tagged.
- Coverage: 24 courses at 8 Irish universities. Results outside this set are not represented.
