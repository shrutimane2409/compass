import { eur } from "../format";
import type { Recommendation } from "../types";

export function Compare({ items, onClear }: { items: Recommendation[]; onClear: () => void }) {
  if (items.length < 2) return null;
  const rows: [string, (r: Recommendation) => string][] = [
    ["University", (r) => `${r.course.university}, ${r.course.city}`],
    ["Eligibility", (r) => r.eligibility.status],
    ["Course fit", (r) => `${Math.round(r.fitScore * 100)}%`],
    ["Tuition", (r) => eur(r.cost.tuition)],
    ["Living", (r) => eur(r.cost.living)],
    ["One-off", (r) => eur(r.cost.oneOff)],
    ["Total", (r) => eur(r.cost.total)],
    ["Duration", (r) => `${r.course.durationMonths} months`],
  ];
  return (
    <section className="panel compare-table">
      <div className="panel-head">
        <h2>Compare</h2>
        <button className="link" onClick={onClear}>Clear</button>
      </div>
      <div className="scroll">
        <table>
          <thead>
            <tr><th></th>{items.map((r) => <th key={r.course.id}>{r.course.title}</th>)}</tr>
          </thead>
          <tbody>
            {rows.map(([label, f]) => (
              <tr key={label}><th>{label}</th>{items.map((r) => <td key={r.course.id}>{f(r)}</td>)}</tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
