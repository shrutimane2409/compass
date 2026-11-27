import { useState } from "react";
import { CGPA_FACTOR } from "../format";
import type { EnglishTest, ExchangeRate } from "../types";

export interface FormState {
  gradeMode: "percent" | "cgpa";
  grade: number;
  background: string;
  englishTest: EnglishTest | "none";
  englishScore: number;
  workYears: number;
  budgetLakh: number;
  inrPerEur: number;
  interests: string[];
}

export const BACKGROUNDS = [
  ["computer-science", "Computer Science / Engineering"],
  ["information-technology", "Information Technology"],
  ["electronics", "Electronics / Telecom"],
  ["electrical", "Electrical"],
  ["mechanical", "Mechanical / Civil"],
  ["mathematics", "Mathematics / Statistics"],
  ["commerce", "Commerce / Business"],
  ["science", "Science (BSc)"],
  ["arts", "Arts / Humanities"],
] as const;

const SUGGESTED = ["machine learning", "data science", "cloud", "cybersecurity", "software engineering", "AI", "business analytics", "NLP"];

export function gradePercent(f: FormState): number {
  return f.gradeMode === "cgpa" ? Math.min(100, Math.round(f.grade * CGPA_FACTOR * 100) / 100) : f.grade;
}

interface Props {
  value: FormState;
  onChange: (next: FormState) => void;
  onSubmit: () => void;
  submitted: boolean;
  fx: ExchangeRate | null;
}

export function ProfileForm({ value: f, onChange, onSubmit, submitted, fx }: Props) {
  const [interestDraft, setInterestDraft] = useState("");
  const set = <K extends keyof FormState>(k: K, v: FormState[K]) => onChange({ ...f, [k]: v });

  const addInterest = (raw: string) => {
    const v = raw.trim();
    if (v && !f.interests.some((i) => i.toLowerCase() === v.toLowerCase()) && f.interests.length < 10)
      set("interests", [...f.interests, v]);
    setInterestDraft("");
  };

  return (
    <form className="panel profile" onSubmit={(e) => { e.preventDefault(); onSubmit(); }}>
      <h2>Your profile</h2>

      <label className="field">
        <span>Bachelor's grade</span>
        <div className="row">
          <input type="number" min={0} max={f.gradeMode === "cgpa" ? 10 : 100} step="0.01" value={f.grade}
                 onChange={(e) => set("grade", Number(e.target.value))} aria-label="Grade" />
          <select value={f.gradeMode} onChange={(e) => set("gradeMode", e.target.value as FormState["gradeMode"])} aria-label="Grade scale">
            <option value="percent">%</option>
            <option value="cgpa">CGPA /10</option>
          </select>
        </div>
        {f.gradeMode === "cgpa" && <small>≈ {gradePercent(f)}% (CGPA × {CGPA_FACTOR}; universities may convert differently)</small>}
      </label>

      <label className="field">
        <span>Degree background</span>
        <select value={f.background} onChange={(e) => set("background", e.target.value)}>
          {BACKGROUNDS.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
        </select>
      </label>

      <label className="field">
        <span>English test</span>
        <div className="row">
          <select value={f.englishTest} onChange={(e) => set("englishTest", e.target.value as FormState["englishTest"])} aria-label="English test">
            <option value="none">Not taken yet</option>
            <option value="Ielts">IELTS</option>
            <option value="Pte">PTE</option>
            <option value="Toefl">TOEFL iBT</option>
          </select>
          {f.englishTest !== "none" && (
            <input type="number" min={0} max={f.englishTest === "Ielts" ? 9 : 120} step={f.englishTest === "Pte" ? 1 : 0.5}
                   value={f.englishScore} onChange={(e) => set("englishScore", Number(e.target.value))} aria-label="Score" />
          )}
        </div>
        {f.englishTest === "Toefl" && (
          <small>Enter 0–120 for tests before 21 January 2026, or the 1–6 band score for newer tests.</small>
        )}
      </label>

      <label className="field">
        <span>Work experience (years)</span>
        <input type="number" min={0} max={50} step="0.5" value={f.workYears} onChange={(e) => set("workYears", Number(e.target.value))} />
      </label>

      <label className="field">
        <span>Total budget for the whole course</span>
        <div className="row">
          <input type="number" min={1} step="0.5" value={f.budgetLakh} onChange={(e) => set("budgetLakh", Number(e.target.value))} aria-label="Budget in lakh" />
          <em className="unit">₹ lakh</em>
        </div>
        <small>
          At ₹<input className="rate" type="number" min={1} step="0.01" value={f.inrPerEur}
                     onChange={(e) => set("inrPerEur", Number(e.target.value))} aria-label="INR per EUR" />
          per € ≈ €{Math.round((f.budgetLakh * 100000) / f.inrPerEur).toLocaleString("en-IE")}
          <br />
          {fx
            ? <>Rate: <a href={fx.sourceUrl} target="_blank" rel="noreferrer">ECB reference rate</a>, {fx.date}{f.inrPerEur !== Math.round(fx.perEur * 100) / 100 && " (edited)"}</>
            : "Enter today's rate"}
        </small>
      </label>

      <div className="field">
        <span>What do you want to study?</span>
        <div className="chips">
          {f.interests.map((i) => (
            <button type="button" key={i} className="chip on" onClick={() => set("interests", f.interests.filter((x) => x !== i))}>
              {i} ×
            </button>
          ))}
        </div>
        <input value={interestDraft} placeholder="Type and press Enter"
               onChange={(e) => setInterestDraft(e.target.value)}
               onKeyDown={(e) => { if (e.key === "Enter") { e.preventDefault(); addInterest(interestDraft); } }} />
        <div className="chips">
          {SUGGESTED.filter((s) => !f.interests.includes(s)).map((s) => (
            <button type="button" key={s} className="chip" onClick={() => addInterest(s)}>+ {s}</button>
          ))}
        </div>
      </div>

      <button className="primary" type="submit">{submitted ? "Update shortlist" : "Find my courses"}</button>
    </form>
  );
}
