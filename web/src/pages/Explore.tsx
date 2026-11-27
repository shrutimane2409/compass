import { useState } from "react";
import type { Shared } from "../App";
import { useAccount } from "../account";
import { Compare } from "../components/Compare";
import { CourseCard } from "../components/CourseCard";
import { Helpful } from "../components/Helpful";
import { ProfileForm } from "../components/ProfileForm";
import { WhatIf } from "../components/WhatIf";
import { DragHandle, usePersistentState, useResizable } from "../layout";
import { toProfile } from "../profile";
import type { Priority } from "../types";

export const PRIORITY_LABEL: Record<Priority, string> = {
  Balanced: "Balanced",
  BestFit: "Best course fit",
  LowestCost: "Lowest cost",
  Reputation: "University reputation",
};

export function Explore({ form, setForm, submitted, submit, data, loading, error, priority, setPriority, fx }: Shared) {
  const { me, saveProfile } = useAccount();
  const [hideIneligible, setHideIneligible] = useState(true);
  const [compareIds, setCompareIds] = useState<number[]>([]);
  const [showForm, setShowForm] = usePersistentState("compass.explore.form", true);
  const formWidth = useResizable("compass.explore.formWidth", 320, 260, 520);
  const items = data?.items ?? [];
  const visible = hideIneligible ? items.filter((r) => r.eligibility.status !== "NotEligible") : items;
  const compared = compareIds.map((id) => items.find((r) => r.course.id === id)).filter((r) => r !== undefined);
  const toggleCompare = (id: number) =>
    setCompareIds((ids) => (ids.includes(id) ? ids.filter((x) => x !== id) : [...ids, id].slice(-3)));

  return (
    <div className={`explore ${showForm ? "with-form" : ""}`} style={{ "--form-w": `${formWidth.size}px` } as React.CSSProperties}>
      {showForm && <aside className="left">
        <ProfileForm value={form} onChange={setForm} onSubmit={() => { submit(); if (me) void saveProfile(toProfile(form), priority); }}
                     submitted={submitted} fx={fx} />
        {data && <WhatIf budgetLakh={form.budgetLakh} onBudget={(v) => setForm({ ...form, budgetLakh: v })} items={items} />}
        <button type="button" className="link small" onClick={() => setShowForm(false)}>Hide profile panel</button>
      </aside>}
      {showForm && <DragHandle label="Resize profile panel" onStart={formWidth.onStart} onDrag={(dx) => formWidth.resizeBy(dx)} onStep={formWidth.step} />}

      <section className="center">
        {!showForm && <button type="button" className="secondary show-form" onClick={() => setShowForm(true)}>Show profile panel</button>}
        {!submitted && (
          <div className="panel empty">
            <h2>Your shortlist will appear here</h2>
            <p>Fill in your profile and press <b>Find my courses</b>. You'll see which courses you can apply to, what they really cost, and why.</p>
          </div>
        )}
        {error && <div className="panel error">{error}</div>}
        {data && (
          <>
            <div className="toolbar">
              <span>{visible.length} courses for the {data.targetIntake} intake {loading && <em className="loading">updating…</em>}</span>
              <label>
                What matters most?{" "}
                <select value={priority} onChange={(e) => setPriority(e.target.value as Priority)} aria-label="Ranking priority">
                  {(Object.keys(PRIORITY_LABEL) as Priority[]).map((p) => <option key={p} value={p}>{PRIORITY_LABEL[p]}</option>)}
                </select>
              </label>
              <label><input type="checkbox" checked={hideIneligible} onChange={(e) => setHideIneligible(e.target.checked)} /> Hide courses I can't apply to</label>
            </div>
            <div className="legend">
              <b>Eligible</b> = every requirement published and met · <b>Check requirements</b> = nothing rules you out, but something isn't
              published or you haven't entered it · <b>Reach</b> = close to a minimum.
              {priority === "Reputation"
                ? " University rankings are now part of the order."
                : " Rankings are shown for information; choose “University reputation” to rank by them."}
            </div>
            {!data.fitAvailable && (
              <div className="notice">
                Course-fit scoring is waking up (Compass runs on free hosting), so for now courses are ranked on eligibility and cost.
                This list refreshes automatically in a few seconds.
              </div>
            )}
            <Compare items={compared} onClear={() => setCompareIds([])} />
            <div className="cards">
              {visible.map((r, i) => (
                <CourseCard key={r.course.id} rec={r} rank={i + 1} inrPerEur={form.inrPerEur}
                            compared={compareIds.includes(r.course.id)} compareDisabled={compareIds.length >= 3}
                            onToggleCompare={() => toggleCompare(r.course.id)} />
              ))}
            </div>
            <Helpful key={data.catalogLoadedAt + data.priority} context="shortlist" label="Was this shortlist useful?" />
          </>
        )}
      </section>

    </div>
  );
}
