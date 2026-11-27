import type { UniversityRank } from "../types";

/** League-table badges with their source and edition. Shown for information, never as a verdict. */
export function Rankings({ ranks }: { ranks: UniversityRank[] | undefined }) {
  if (!ranks?.length) return <p className="ranks none">Not in the QS or THE world rankings</p>;
  const latest = (source: string) => ranks.filter((r) => r.source === source).sort((a, b) => b.edition - a.edition)[0];
  const shown = [latest("QS"), latest("THE")].filter(Boolean);
  return (
    <p className="ranks" title="Rankings mostly measure research and reputation, not teaching quality or job outcomes.">
      {shown.map((r) => (
        <a key={r.source} href={r.sourceUrl} target="_blank" rel="noreferrer" className="rank-badge">
          {r.source} {r.edition}: <b>#{r.rankText.replace(/^#/, "")}</b>
        </a>
      ))}
      <span className="rank-note">World rankings mostly measure research and reputation</span>
    </p>
  );
}
