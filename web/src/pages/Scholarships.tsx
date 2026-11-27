import { useEffect, useState } from "react";
import type { Shared } from "../App";
import { useAccount } from "../account";
import { matchScholarships } from "../api";
import { toProfile } from "../profile";
import type { ScholarshipMatch } from "../types";

const GROUPS: { fit: ScholarshipMatch["fit"]; title: string; hint: string }[] = [
  { fit: "Likely", title: "Considered automatically", hint: "You're assessed when you apply. No extra form, but awards aren't guaranteed." },
  { fit: "Possible", title: "Worth applying for", hint: "Needs a separate application, usually once you hold an offer. Some are highly competitive." },
  { fit: "NotApplicable", title: "Not for your current list", hint: "Tied to other universities or courses, or a requirement you don't meet." },
];

export function Scholarships({ form, submitted, courses }: Shared) {
  const { me } = useAccount();
  const [matches, setMatches] = useState<ScholarshipMatch[] | null>(null);
  const [error, setError] = useState("");
  const savedIds = me?.saved.map((s) => s.courseId) ?? [];
  const basis = savedIds.length ? "your saved courses" : "every course you can apply to";

  useEffect(() => {
    matchScholarships(toProfile(form), savedIds.length ? savedIds : null)
      .then((m) => { setMatches(m); setError(""); })
      .catch((e) => setError((e as Error).message));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [form, savedIds.join(",")]);

  return (
    <div className="page">
      <h1>Scholarships</h1>
      <p className="muted">
        Matched to {basis}{submitted ? "" : " using the default profile (fill in yours in Explore for a better match)"}.
        Every entry links to the official page: amounts and deadlines change each year, so check there before you apply.
      </p>
      {error && <div className="panel error">{error}</div>}
      {matches?.length === 0 && <p className="muted">No scholarship data loaded yet.</p>}
      {matches && GROUPS.map((g) => {
        const list = matches.filter((m) => m.fit === g.fit);
        if (!list.length) return null;
        const body = list.map((m) => (
          <article key={m.scholarship.slug} className="panel scholarship">
            <div className="row-between">
              <div>
                <h3>{m.scholarship.name}</h3>
                <span className="muted small">{m.scholarship.provider}</span>
              </div>
              <b className="amount">{m.scholarship.amountText}</b>
            </div>
            <ul className="reasons small">
              {m.reasons.map((r) => <li key={r}>{r}</li>)}
              {m.fit !== "NotApplicable" && m.courseIds.length > 0 && (
                <li className="muted">{m.courseIds.slice(0, 3).map((id) => courses.get(id)?.title).filter(Boolean).join(", ")}{m.courseIds.length > 3 ? "…" : ""}</li>
              )}
            </ul>
            <p className="small"><b>How to apply:</b> {m.scholarship.howToApply} <b>Deadline:</b> {m.scholarship.deadlineText}.</p>
            <a className="small" href={m.scholarship.sourceUrl} target="_blank" rel="noreferrer">Official page ↗</a>
            <span className="muted small"> · checked {new Date(m.scholarship.verifiedAt).toLocaleDateString("en-IE", { month: "short", year: "numeric" })}</span>
          </article>
        ));
        return (
          <section key={g.fit} className="group">
            {g.fit === "NotApplicable"
              ? <details><summary><h2>{g.title} ({list.length})</h2></summary><p className="muted small">{g.hint}</p>{body}</details>
              : <><h2>{g.title} ({list.length})</h2><p className="muted small">{g.hint}</p>{body}</>}
          </section>
        );
      })}
    </div>
  );
}
