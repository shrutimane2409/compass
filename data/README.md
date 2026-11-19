# Data

| File | Contents |
|---|---|
| `courses.csv` | 24 taught master's courses across 8 universities (non-EU fees). `english_source_url` is set when the English minimums come from a university-wide page rather than `source_url` |
| `living_costs.csv` | Monthly living costs by city and category |
| `one_off_costs.csv` | One-off costs for Ireland (IRP, insurance, deposit, set-up) |
| `sources.txt` | Official pages to ingest for retrieval |
| `snapshots/` | Saved copies of official pages that block automated fetching (see its README) |

## Rules

- Every value comes from an official university or government page, recorded in `source_url`.
- `verified_at` is the date the value was checked.
- `fee_academic_year` is the year the fee applies to; blank when the page does not say.
- `is_estimate = true` when any value in the row is derived rather than stated (a range midpoint,
  a duration not stated on the page, or a fee from an older academic year).
- Leave a column empty when the page does not state it. Never guess.

`accepted_backgrounds` uses `|` as a separator, e.g. `computer-science|information-technology`.
Values are the background codes the web form offers (`web/src/components/ProfileForm.tsx`).

## Entry requirements (checked 2026-11-19)

Each row records the **standard entry route** on the course page: degree class and named disciplines.
`entry_source_url` is set when that text is not on `source_url` (TCD, UL, Maynooth).

**Backgrounds.** Disciplines the page names are mapped to background codes. "Any discipline" means all nine codes.
Conversion courses list every code except the one they exclude (UCD Conversion excludes Computer Science;
UCC ISBP excludes CS and IT). `backgrounds_open = true` when the page also accepts "related", "cognate" or
"such as" disciplines. The rules engine then reports an unnamed background as borderline ("Reach": not named,
check with the university) instead of "not eligible". Maynooth Data Science sets its bar in maths modules, not degree
disciplines, so its backgrounds are blank.

**Minimum grade.** Course pages give an Irish degree class (2.2 / 2.1 / 1st). `min_grade_percent` is that class
converted with the university's own India table, recorded in `grade_source_url`:

| University | India conversion (bachelor's %) | Used |
|---|---|---|
| UCC | 2H2 55 · 2H1 65 · 1H 75 | as stated |
| Galway | 2:2 55 · 2:1 65 · 1st 75 (top-100 NIRF: 52 · 62 · 70) | top-100 NIRF figure (lowest), `is_estimate` |
| TU Dublin | 2.2 50/55 · 2.1 65/70 (Tier 1 / Tier 2, tiers not defined on the page) | Tier 1 (lowest), `is_estimate` |
| UL | "55-70% depending on the programme" | 55 (lower bound), `is_estimate` |
| TCD Business School | "India: CGPA 6.8/10" | 6.8 × 9.5 = 64.6 (the app's CGPA factor), `is_estimate` |
| UCD, DCU, TCD Computer Science, Maynooth | no India conversion published | blank |

Where a university gives more than one figure, the lowest one is used. Overstating a minimum would wrongly mark
eligible students as ineligible, which is the worse error for a shortlist. The borderline margin (3%) still applies.

`min_work_years` is 0 everywhere: no standard route requires work experience.

## Known gaps (next curation pass)

- Grade minimums are blank for UCD (5), DCU (2), TCD Computer Science and Maynooth (2). None of them publishes an
  India conversion (UCD's India page covers undergraduate entry only). The rules engine reports "not stated".
  Maynooth Data Science asks for "2H1 with an overall score of 65%", but that is on the Irish scale, not an Indian
  conversion.
- Alternative routes are not modelled: 2.2 + work experience (UCD Advanced AI, TU Dublin Data Science and Data
  Analytics), work experience instead of the grade (UCD Negotiated Learning: 3 years), RPL routes (UCC, Maynooth),
  and Galway's "good Second Class Honours on the Programme Director's recommendation".
- Module-level requirements are not modelled: ML and programming modules (UCD Advanced AI), ≥50% CS credits (UCC
  Computing Science, Cybersecurity), 30 ECTS quantitative content (UCC AI for Business), maths modules at 60%
  (Maynooth Data Science), mandatory CS modules (Maynooth CS Applied).
- UCC ISBP also excludes applicants with more than 2 years of software-development work. The engine only models
  a minimum.
- TCD's "MSc Business Analytics" has been renamed "MSc in Business Analytics and AI for Management" (2026 intake).
  The row keeps the old title until its fee and duration are re-checked.
- `application_deadline` is still blank for 23 courses.
- English minimums (`min_ielts`, `min_pte`, `min_toefl`, overall scores) are filled for all 24 courses (checked 2026-11-19).
  Galway states all three on its course pages. Elsewhere the course page defers to a university-wide page,
  recorded in `english_source_url`: UCD Registry, UCC, DCU (Faculty of Engineering & Computing), UL, Maynooth,
  TCD (SCSS / Business School), TU Dublin. TU Dublin's TOEFL/PTE come from its official score comparison table
  (IELTS 6.0 = TOEFL 80 = PTE 65; 6.5 = 92 = 70).
  UL's page says "some programmes have a higher IELTS requirement"; the UL course pages state none, so 6.5 is used.
- `min_toefl` is the TOEFL iBT minimum on the 0-120 scale (tests before 21 January 2026); `min_toefl_new` is the
  overall minimum on the 1-6 scale used since then. `min_toefl_new` is filled for 19 courses (4.5; Maynooth 5).
  It is blank where the page gives no new-scale figure: Galway ×2, TCD Business Analytics, and TU Dublin's two
  IELTS-6.5 courses (its comparison table gives only the old-scale 92). The rules engine treats a TOEFL score
  of 6 or less as the new scale and reports "not stated" when `min_toefl_new` is blank.
- Per-band minimums (e.g. "no band below 6.0", "6.5 in Writing", PTE "no section below 59") are not modelled yet.
- Maynooth living costs have no per-person utilities figure (the source gives a per-household range).
- Living costs for Dublin use UCD's median estimates (April 2026); other cities use their university's ranges.
