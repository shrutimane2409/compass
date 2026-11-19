import { useEffect, useMemo, useRef, useState } from "react";
import { getRecommendations } from "./api";
import { Chat } from "./components/Chat";
import { Compare } from "./components/Compare";
import { CourseCard } from "./components/CourseCard";
import { gradePercent, ProfileForm, type FormState } from "./components/ProfileForm";
import { WhatIf } from "./components/WhatIf";
import type { RecommendationResponse, StudentProfile } from "./types";

const INITIAL: FormState = {
  gradeMode: "percent",
  grade: 70,
  background: "computer-science",
  englishTest: "none",
  englishScore: 7,
  workYears: 0,
  budgetLakh: 35,
  inrPerEur: 100,
  interests: ["machine learning"],
};

function toProfile(f: FormState): StudentProfile {
  return {
    gradePercent: gradePercent(f),
    background: f.background,
    english: f.englishTest === "none" ? null : { test: f.englishTest, score: f.englishScore },
    workYears: f.workYears,
    budgetEur: Math.max(1, Math.round((f.budgetLakh * 100000) / f.inrPerEur)),
    interests: f.interests,
  };
}

export default function App() {
  const [form, setForm] = useState<FormState>(INITIAL);
  const [submitted, setSubmitted] = useState(false);
  const [data, setData] = useState<RecommendationResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [hideIneligible, setHideIneligible] = useState(true);
  const [compareIds, setCompareIds] = useState<number[]>([]);
  const inflight = useRef<AbortController | null>(null);

  const profile = useMemo(() => toProfile(form), [form]);

  // After the first search, every change re-ranks (debounced). The API answers from memory, so this stays fast.
  useEffect(() => {
    if (!submitted) return;
    const timer = setTimeout(async () => {
      inflight.current?.abort();
      const ctrl = new AbortController();
      inflight.current = ctrl;
      setLoading(true);
      try {
        setData(await getRecommendations(profile, ctrl.signal));
        setError("");
      } catch (e) {
        if ((e as Error).name !== "AbortError") setError((e as Error).message);
      } finally {
        if (inflight.current === ctrl) setLoading(false);
      }
    }, 250);
    return () => clearTimeout(timer);
  }, [profile, submitted]);

  const items = data?.items ?? [];
  const visible = hideIneligible ? items.filter((r) => r.eligibility.status !== "NotEligible") : items;
  const compared = compareIds.map((id) => items.find((r) => r.course.id === id)).filter((r) => r !== undefined);
  const toggleCompare = (id: number) =>
    setCompareIds((ids) => (ids.includes(id) ? ids.filter((x) => x !== id) : [...ids, id].slice(-3)));

  return (
    <div className="app">
      <header className="top">
        <div className="brand"><span className="logo">◎</span> Compass</div>
        <p>Independent course matching for Indian students choosing a master's in Ireland. No commissions, every fact sourced.</p>
      </header>

      <main className="layout">
        <aside className="left">
          <ProfileForm value={form} onChange={setForm} onSubmit={() => setSubmitted(true)} submitted={submitted} />
          {data && <WhatIf budgetLakh={form.budgetLakh} onBudget={(v) => setForm({ ...form, budgetLakh: v })} items={items} />}
        </aside>

        <section className="center">
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
                <span>{visible.length} courses {loading && <em className="loading">updating…</em>}</span>
                <label><input type="checkbox" checked={hideIneligible} onChange={(e) => setHideIneligible(e.target.checked)} /> Hide courses I can't apply to</label>
              </div>
              {!data.fitAvailable && <div className="notice">Course-fit scoring is temporarily unavailable, so courses are ranked on eligibility and cost only.</div>}
              <Compare items={compared} onClear={() => setCompareIds([])} />
              <div className="cards">
                {visible.map((r, i) => (
                  <CourseCard key={r.course.id} rec={r} rank={i + 1} inrPerEur={form.inrPerEur}
                              compared={compareIds.includes(r.course.id)} compareDisabled={compareIds.length >= 3}
                              onToggleCompare={() => toggleCompare(r.course.id)} />
                ))}
              </div>
            </>
          )}
        </section>

        <aside className="right">
          <Chat profile={submitted ? profile : null} />
        </aside>
      </main>
    </div>
  );
}
