import { useState } from "react";
import { sendFeedback, type FeedbackField } from "../api";

const FIELDS: [FeedbackField, string][] = [
  ["fee", "Fee"],
  ["entry-requirements", "Entry requirements"],
  ["english", "English requirement"],
  ["deadline", "Deadline"],
  ["living-costs", "Living costs"],
  ["other", "Something else"],
];

/** Lets students flag wrong information. Reports are reviewed against the official page before any data changes. */
export function ReportError({ courseId }: { courseId: number }) {
  const [open, setOpen] = useState(false);
  const [field, setField] = useState<FeedbackField>("fee");
  const [message, setMessage] = useState("");
  const [state, setState] = useState<"idle" | "sending" | "sent" | "error">("idle");
  const [error, setError] = useState("");

  if (state === "sent") return <span className="report-done">Thanks. We'll check this against the official page.</span>;
  if (!open) return <button className="link small" onClick={() => setOpen(true)}>Report an error</button>;

  return (
    <form className="report" onSubmit={async (e) => {
      e.preventDefault();
      setState("sending");
      try {
        await sendFeedback(courseId, field, message);
        setState("sent");
      } catch (err) {
        setError((err as Error).message);
        setState("error");
      }
    }}>
      <select value={field} onChange={(e) => setField(e.target.value as FeedbackField)} aria-label="What is wrong">
        {FIELDS.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
      </select>
      <input value={message} onChange={(e) => setMessage(e.target.value)} maxLength={1000} required
             placeholder="What's wrong, and where did you see the correct value?" />
      <button className="primary small" disabled={state === "sending" || !message.trim()}>Send</button>
      <button type="button" className="link small" onClick={() => setOpen(false)}>Cancel</button>
      {state === "error" && <span className="error">{error}</span>}
    </form>
  );
}
