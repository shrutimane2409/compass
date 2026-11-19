import type { Recommendation } from "../types";

interface Props {
  budgetLakh: number;
  onBudget: (v: number) => void;
  items: Recommendation[];
}

export function WhatIf({ budgetLakh, onBudget, items }: Props) {
  const eligible = items.filter((r) => r.eligibility.status !== "NotEligible");
  const affordable = eligible.filter((r) => r.affordability === "Affordable").length;
  const stretch = eligible.filter((r) => r.affordability === "Stretch").length;
  return (
    <section className="panel whatif">
      <h2>What if…</h2>
      <label className="field">
        <span>My budget were ₹{budgetLakh} lakh</span>
        <input type="range" min={10} max={80} step={1} value={budgetLakh} onChange={(e) => onBudget(Number(e.target.value))} />
      </label>
      <p className="stat"><b>{affordable}</b> affordable · <b>{stretch}</b> a stretch · of {eligible.length} you can apply to</p>
    </section>
  );
}
