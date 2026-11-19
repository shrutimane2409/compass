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

## Known gaps (next curation pass)

- Entry requirements (minimum grade for Indian degrees, IELTS/PTE/TOEFL, accepted backgrounds) are mostly blank.
  The rules engine reports these as "not stated" rather than guessing.
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
