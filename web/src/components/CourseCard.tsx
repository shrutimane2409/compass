import { eur, inrLakh, TAG_LABEL } from "../format";
import type { Recommendation } from "../types";

interface Props {
  rec: Recommendation;
  rank: number;
  inrPerEur: number;
  compared: boolean;
  onToggleCompare: () => void;
  compareDisabled: boolean;
}

const mark = (p: boolean | null) => (p === true ? "✓" : p === false ? "✗" : "?");

export function CourseCard({ rec, rank, inrPerEur, compared, onToggleCompare, compareDisabled }: Props) {
  const { course: c, cost } = rec;
  const verified = new Date(c.verifiedAt).toLocaleDateString("en-IE", { day: "numeric", month: "short", year: "numeric" });

  return (
    <article className={`card ${rec.eligibility.status === "NotEligible" ? "muted" : ""}`}>
      <header>
        <span className="rank">{rank}</span>
        <div>
          <h3>{c.title}</h3>
          <p className="sub">{c.university} · {c.city} · {c.durationMonths} months</p>
        </div>
        <label className="compare">
          <input type="checkbox" checked={compared} disabled={!compared && compareDisabled} onChange={onToggleCompare} /> Compare
        </label>
      </header>

      <div className="tags">
        {rec.tags.map((t) => <span key={t} className={`tag t-${t}`}>{TAG_LABEL[t] ?? t}</span>)}
      </div>

      <div className="cost">
        <div>
          <strong>{eur(cost.total)}</strong>
          <span>{inrLakh(cost.total, inrPerEur)} estimated total</span>
        </div>
        <dl>
          <div><dt>Tuition{c.feeAcademicYear ? ` (${c.feeAcademicYear})` : ""}</dt><dd>{eur(cost.tuition)}</dd></div>
          <div><dt>Living × {c.durationMonths} mo</dt><dd>{eur(cost.living)}</dd></div>
          <div><dt>One-off</dt><dd>{eur(cost.oneOff)}</dd></div>
        </dl>
      </div>

      <details>
        <summary>Why this result</summary>
        <ul className="checks">
          {rec.eligibility.checks.map((ch) => (
            <li key={ch.name} className={ch.passed === true ? "ok" : ch.passed === false ? "bad" : "unknown"}>
              <b>{mark(ch.passed)}</b> {ch.detail}
            </li>
          ))}
          {rec.reasons.filter((r) => !rec.eligibility.checks.some((ch) => ch.detail === r)).map((r) => <li key={r}>{r}</li>)}
        </ul>
      </details>

      <footer>
        <a href={c.sourceUrl} target="_blank" rel="noreferrer">Official source ↗</a>
        {c.englishSourceUrl && (
          <a href={c.englishSourceUrl} target="_blank" rel="noreferrer">English requirement ↗</a>
        )}
        {c.entrySourceUrl && (
          <a href={c.entrySourceUrl} target="_blank" rel="noreferrer">Entry requirements ↗</a>
        )}
        {c.gradeSourceUrl && (
          <a href={c.gradeSourceUrl} target="_blank" rel="noreferrer">India grade conversion ↗</a>
        )}
        <span>Verified {verified}</span>
      </footer>
    </article>
  );
}
