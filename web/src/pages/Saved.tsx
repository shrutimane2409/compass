import type { Shared } from "../App";
import { useAccount } from "../account";
import { HeartButton } from "../components/Icons";
import { Rankings } from "../components/Rankings";
import { SignInPrompt } from "../components/SignInPrompt";
import { eur, TAG_LABEL } from "../format";

export function Saved({ data, courses }: Shared) {
  const account = useAccount();
  if (!account.me) {
    return (
      <div className="page">
        <h1>Saved courses</h1>
        <SignInPrompt title="Keep a shortlist" text="Sign in with Google to save courses you like and come back to them on any device." />
      </div>
    );
  }
  const recs = new Map((data?.items ?? []).map((r) => [r.course.id, r]));
  const saved = account.me.saved.map((s) => ({ s, c: courses.get(s.courseId), r: recs.get(s.courseId) })).filter((x) => x.c);

  return (
    <div className="page">
      <h1>Saved courses</h1>
      {saved.length === 0 && <p className="muted">Tap the heart on any course in <a href="#/explore">Explore</a> to save it here.</p>}
      <div className="grid-2">
        {saved.map(({ s, c, r }) => (
          <article key={s.courseId} className="panel saved-card">
            <div className="panel-head">
              <span className="muted small">{c!.university} · {c!.city}</span>
              <HeartButton on onClick={() => account.toggleSaved(s.courseId)} label="Remove from saved" />
            </div>
            <h3>{c!.title}</h3>
            <Rankings ranks={c!.rankings} />
            <div className="row-between">
              <span>{r ? `${eur(r.cost.total)} total` : `${eur(c!.feeNonEuEur)} tuition`}</span>
              {r && <span className={`tag t-${r.tags[0]}`}>{TAG_LABEL[r.tags[0]]}</span>}
            </div>
            <div className="row-between small">
              <a href={c!.entrySourceUrl ?? c!.sourceUrl} target="_blank" rel="noreferrer">Official page ↗</a>
              {account.application(s.courseId)
                ? <a href="#/applications">In your applications</a>
                : <button type="button" className="link small" onClick={() => account.track(s.courseId)}>Track application</button>}
            </div>
          </article>
        ))}
      </div>
    </div>
  );
}
