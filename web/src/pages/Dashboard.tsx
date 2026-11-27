import { useEffect, useState } from "react";
import type { Shared } from "../App";
import { useAccount } from "../account";
import { matchScholarships } from "../api";
import { CHECKLIST } from "../checklist";
import { GoogleButton } from "../components/GoogleButton";
import { HeartButton } from "../components/Icons";
import { eur, TAG_LABEL } from "../format";
import { toProfile } from "../profile";
import type { Course, ScholarshipMatch } from "../types";

const STEPS = ["Shortlist", "Apply", "Offers", "Visa", "Arrive"];

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString("en-IE", { day: "numeric", month: "short", year: "numeric" });
}

function greeting() {
  const h = new Date().getHours();
  return h < 12 ? "Good morning" : h < 18 ? "Good afternoon" : "Good evening";
}

export function Dashboard({ form, submitted, data, courses }: Shared) {
  const account = useAccount();
  const me = account.me;
  const [scholarships, setScholarships] = useState<ScholarshipMatch[] | null>(null);
  const savedIds = me?.saved.map((s) => s.courseId) ?? [];

  useEffect(() => {
    if (!submitted) return;
    matchScholarships(toProfile(form), savedIds.length ? savedIds : null).then(setScholarships).catch(() => setScholarships(null));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [submitted, form, savedIds.join(",")]);

  if (!me) {
    return (
      <div className="page">
        <section className="hero panel">
          <h1>Choose your master's in Ireland with facts, not sales pitches</h1>
          <p>Compass checks your eligibility against each university's published requirements, works out the real total cost,
            and explains every result with a link to the official page. No consultancy commissions.</p>
          <div className="hero-actions">
            <a className="button primary" href="#/explore">Find my courses</a>
            <GoogleButton />
          </div>
          <p className="muted small">Sign in (optional) to save your profile, keep a shortlist, track applications and work through the pre-arrival checklist.</p>
        </section>
        <Journey reached={0} />
      </div>
    );
  }

  const tracked = me.applications;
  const reached =
    (me.checklistDone.includes("irp") ? 5 : 0) ||
    (me.checklistDone.includes("visa") ? 4 : 0) ||
    (tracked.some((a) => a.status === "offer" || a.status === "accepted") ? 3 : 0) ||
    (tracked.some((a) => a.status === "applied") ? 2 : 0) ||
    (me.saved.length > 0 ? 1 : 0);

  const today = new Date().toISOString().slice(0, 10);
  const watching = new Set([...savedIds, ...tracked.map((a) => a.courseId)]);
  const deadlines = [...watching]
    .map((id) => courses.get(id))
    .filter((c): c is Course => !!c?.applicationDeadline && c.applicationDeadline >= today)
    .sort((a, b) => a.applicationDeadline!.localeCompare(b.applicationDeadline!));
  const rolling = [...watching].map((id) => courses.get(id)).filter((c): c is Course => !!c && !c.applicationDeadline);
  const done = CHECKLIST.filter((i) => me.checklistDone.includes(i.id)).length;
  const top = (data?.items ?? []).filter((r) => r.eligibility.status !== "NotEligible").slice(0, 4);
  const matched = scholarships?.filter((s) => s.fit !== "NotApplicable") ?? [];
  const firstName = (me.user.name ?? "").split(" ")[0];

  return (
    <div className="page">
      <div className="page-head">
        <div>
          {data && <p className="muted">Target intake: {data.targetIntake}</p>}
          <h1>{greeting()}{firstName ? `, ${firstName}` : ""}</h1>
        </div>
        <a className="button primary" href="#/explore">Find more courses</a>
      </div>
      <Journey reached={reached} />

      <div className="stats">
        <a className="stat" href="#/saved"><span>Saved courses</span><b>{me.saved.length}</b></a>
        <a className="stat" href="#/applications"><span>Applications</span><b>{tracked.length}</b></a>
        <a className="stat" href="#/applications">
          <span>Next deadline</span>
          {deadlines[0] ? <><b className="small-b">{formatDate(deadlines[0].applicationDeadline!)}</b><em>{deadlines[0].university}</em></> : <b className="small-b">None set</b>}
        </a>
        <a className="stat" href="#/prearrival">
          <span>Pre-arrival</span><b>{done} / {CHECKLIST.length}</b>
          <i className="bar"><i style={{ width: `${(done / CHECKLIST.length) * 100}%` }} /></i>
        </a>
      </div>

      <div className="dash-grid">
        <section className="panel">
          <div className="panel-head"><h2>Your top matches</h2>{data && <span className="muted small">From your profile</span>}</div>
          {!me.profile && !submitted && <p className="muted">Fill in your profile in <a href="#/explore">Explore</a> once and your matches will appear here every time.</p>}
          <ul className="list">
            {top.map((r) => (
              <li key={r.course.id}>
                <div className="grow">
                  <b>{r.course.title}</b>
                  <span className="muted small">{r.course.university} · {eur(r.cost.total)} total</span>
                </div>
                <span className={`tag t-${r.tags[0]}`}>{TAG_LABEL[r.tags[0]]}</span>
                <HeartButton on={account.isSaved(r.course.id)} onClick={() => account.toggleSaved(r.course.id)}
                             label={account.isSaved(r.course.id) ? "Remove from saved" : "Save course"} />
              </li>
            ))}
          </ul>
        </section>

        <div className="stack">
          <section className="panel">
            <h2>Upcoming</h2>
            {deadlines.length === 0 && rolling.length === 0 && <p className="muted small">Save or track courses to see their deadlines here.</p>}
            <ul className="list compact">
              {deadlines.slice(0, 4).map((c) => (
                <li key={c.id}><span className="date">{formatDate(c.applicationDeadline!)}</span><span className="grow">{c.university}: {c.title}</span></li>
              ))}
              {rolling.length > 0 && (
                <li><span className="date muted">Rolling</span><span className="grow">{rolling.length} course{rolling.length > 1 ? "s" : ""} with no published deadline: apply early</span></li>
              )}
            </ul>
          </section>
          {matched.length > 0 && (
            <section className="panel accent">
              <h2>{matched.length} scholarship{matched.length > 1 ? "s" : ""} for your courses</h2>
              <p className="small">{matched[0].scholarship.name}: {matched[0].scholarship.amountText}.</p>
              <a href="#/scholarships">See all and how to apply</a>
            </section>
          )}
        </div>
      </div>
    </div>
  );
}

function Journey({ reached }: { reached: number }) {
  return (
    <ol className="journey" aria-label="Your journey">
      {STEPS.map((s, i) => (
        <li key={s} className={i < reached ? "done" : i === reached ? "current" : ""}>
          <span>{i + 1}</span>{s}
        </li>
      ))}
    </ol>
  );
}
