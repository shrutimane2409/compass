import { useState } from "react";
import { sendHelpful } from "../api";

export function Helpful({ context, label }: { context: "shortlist" | "chat"; label: string }) {
  const [sent, setSent] = useState<boolean | null>(null);
  if (sent !== null) return <div className="helpful done">Thanks, that helps us improve Compass.</div>;
  const vote = (v: boolean) => { setSent(v); sendHelpful(v, context); };
  return (
    <div className="helpful">
      {label}
      <button type="button" onClick={() => vote(true)} aria-label="Yes, helpful">Yes</button>
      <button type="button" onClick={() => vote(false)} aria-label="No, not helpful">No</button>
    </div>
  );
}
