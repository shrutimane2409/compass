import { useState } from "react";
import type { Shared } from "../App";
import { useAccount } from "../account";
import { DOCUMENTS, STATUSES } from "../checklist";
import { SignInPrompt } from "../components/SignInPrompt";
import type { Application, AppStatus, Course } from "../types";

export function Applications({ courses }: Shared) {
  const account = useAccount();
  if (!account.me) {
    return (
      <div className="page">
        <h1>Applications</h1>
        <SignInPrompt title="Track every application" text="Sign in to move courses from researching to offer, tick off documents and keep notes." />
      </div>
    );
  }
  const apps = account.me.applications;

  return (
    <div className="page">
      <h1>Applications</h1>
      <p className="muted">Move each course along as you go. Deadlines come from the official course page; most Irish master's courses admit on a rolling basis, so earlier is better.</p>
      {apps.length === 0 && <p className="muted">Use <b>Track application</b> on a course in <a href="#/explore">Explore</a> or <a href="#/saved">Saved</a> to start.</p>}
      <div className="board">
        {STATUSES.map((st) => {
          const col = apps.filter((a) => a.status === st.id);
          return (
            <section key={st.id} className="column" aria-label={st.label}>
              <h2>{st.label} <span className="muted">{col.length}</span></h2>
              {col.map((a) => <AppCard key={a.courseId} app={a} course={courses.get(a.courseId)} />)}
            </section>
          );
        })}
      </div>
    </div>
  );
}

function AppCard({ app, course }: { app: Application; course: Course | undefined }) {
  const { updateApplication, untrack } = useAccount();
  const [open, setOpen] = useState(false);
  const [notes, setNotes] = useState(app.notes ?? "");
  const done = DOCUMENTS.filter((d) => app.documents[d.id]).length;

  return (
    <article className="app-card">
      <b>{course?.title ?? `Course ${app.courseId}`}</b>
      <span className="muted small">
        {course?.university}{course?.applicationDeadline ? ` · due ${new Date(course.applicationDeadline).toLocaleDateString("en-IE", { day: "numeric", month: "short" })}` : " · rolling"}
      </span>
      <div className="row-between small">
        <span>Documents {done}/{DOCUMENTS.length}</span>
        <button type="button" className="link small" onClick={() => setOpen(!open)} aria-expanded={open}>{open ? "Close" : "Edit"}</button>
      </div>
      <i className="bar"><i style={{ width: `${(done / DOCUMENTS.length) * 100}%` }} /></i>
      {open && (
        <div className="app-edit">
          <label className="field">
            <span>Status</span>
            <select value={app.status} onChange={(e) => updateApplication({ ...app, status: e.target.value as AppStatus })}>
              {STATUSES.map((s) => <option key={s.id} value={s.id}>{s.label}</option>)}
            </select>
          </label>
          <fieldset>
            <legend>Documents</legend>
            {DOCUMENTS.map((d) => (
              <label key={d.id} className="check">
                <input type="checkbox" checked={!!app.documents[d.id]}
                       onChange={(e) => updateApplication({ ...app, documents: { ...app.documents, [d.id]: e.target.checked } })} />
                {d.label}
              </label>
            ))}
          </fieldset>
          <label className="field">
            <span>Notes</span>
            <textarea rows={3} maxLength={2000} value={notes} onChange={(e) => setNotes(e.target.value)}
                      onBlur={() => notes !== (app.notes ?? "") && updateApplication({ ...app, notes })} />
          </label>
          <div className="row-between small">
            {course && <a href={course.entrySourceUrl ?? course.sourceUrl} target="_blank" rel="noreferrer">Course page ↗</a>}
            <button type="button" className="link small danger" onClick={() => untrack(app.courseId)}>Remove</button>
          </div>
        </div>
      )}
    </article>
  );
}
