import { useAccount } from "../account";
import { eur, inrLakh, pct, TAG_HINT, TAG_LABEL } from "../format";
import { HeartButton } from "./Icons";
import { Rankings } from "./Rankings";
import type { CheckState, Recommendation } from "../types";
import { ReportError } from "./ReportError";

interface Props {
  rec: Recommendation;
  rank: number;
  inrPerEur: number;
  compared: boolean;
  onToggleCompare: () => void;
  compareDisabled: boolean;
}

const STATE: Record<CheckState, { mark: string; cls: string; label: string }> = {
  Met: { mark: "✓", cls: "ok", label: "Met" },
  NotMet: { mark: "✗", cls: "bad", label: "Not met" },
  Borderline: { mark: "~", cls: "unknown", label: "Borderline" },
  NotPublished: { mark: "?", cls: "unknown", label: "Not published" },
  StudentPending: { mark: "…", cls: "pending", label: "Your details needed" },
};

export function CourseCard({ rec, rank, inrPerEur, compared, onToggleCompare, compareDisabled }: Props) {
  const { course: c, cost, breakdown: b } = rec;
  const verified = new Date(c.verifiedAt).toLocaleDateString("en-IE", { day: "numeric", month: "short", year: "numeric" });
  const checkDetails = new Set(rec.eligibility.checks.map((ch) => ch.detail));
  const account = useAccount();
  const saved = account.isSaved(c.id);
  const tracked = !!account.application(c.id);
  const save = () => (account.me ? account.toggleSaved(c.id) : (location.hash = "#/saved"));

  return (
    <article className={`card ${rec.eligibility.status === "NotEligible" ? "muted" : ""}`}>
      <header>
        <span className="rank">{rank}</span>
        <div>
          <h3>{c.title}</h3>
          <p className="sub">{c.university} · {c.city} · {c.durationMonths} months</p>
        </div>
        <div className="card-actions">
          <label className="compare">
            <input type="checkbox" checked={compared} disabled={!compared && compareDisabled} onChange={onToggleCompare} /> Compare
          </label>
          <HeartButton on={saved} onClick={save} label={account.me ? (saved ? "Remove from saved" : "Save course") : "Sign in to save courses"} />
        </div>
      </header>
      <Rankings ranks={c.rankings} />

      <div className="tags">
        {rec.tags.map((t) => <span key={t} className={`tag t-${t}`} title={TAG_HINT[t]}>{TAG_LABEL[t] ?? t}</span>)}
      </div>

      <div className="cost">
        <div>
          <strong>{eur(cost.total)}</strong>
          <span>{inrLakh(cost.total, inrPerEur)} estimated total</span>
        </div>
        <dl>
          <div><dt>Tuition{c.feeAcademicYear ? ` (${c.feeAcademicYear})` : " (year not stated)"}</dt><dd>{eur(cost.tuition)}</dd></div>
          <div><dt>Living × {c.durationMonths} mo</dt><dd>{eur(cost.living)}</dd></div>
          <div><dt>One-off</dt><dd>{eur(cost.oneOff)}</dd></div>
        </dl>
      </div>

      <details>
        <summary>Why this result</summary>
        <ul className="checks">
          {rec.eligibility.checks.map((ch) => {
            const s = STATE[ch.state];
            return (
              <li key={ch.name} className={s.cls}>
                <b title={s.label}>{s.mark}</b> <span className="check-name">{ch.name}:</span> {ch.detail}
                {ch.sourceUrl && <> <a href={ch.sourceUrl} target="_blank" rel="noreferrer">source ↗</a></>}
              </li>
            );
          })}
          {rec.reasons.filter((r) => !checkDetails.has(r)).map((r) => <li key={r} className="note">{r}</li>)}
        </ul>
        <p className="breakdown">
          Ranked on course fit {pct(b.fit)} × {pct(b.weights.fit)}, affordability {pct(b.affordability)} × {pct(b.weights.affordability)},
          eligibility {pct(b.eligibility)} × {pct(b.weights.eligibility)}
          {b.weights.reputation > 0 && <>, reputation {pct(b.reputation)} × {pct(b.weights.reputation)}</>} = <b>{pct(b.total)}</b>
        </p>
      </details>

      <footer>
        <a href={c.sourceUrl} target="_blank" rel="noreferrer">Official course page ↗</a>
        <span>Verified {verified}</span>
        {account.me && (tracked
          ? <a href="#/applications">In your applications</a>
          : <button type="button" className="link small" onClick={() => account.track(c.id)}>Track application</button>)}
        <ReportError courseId={c.id} />
      </footer>
    </article>
  );
}
