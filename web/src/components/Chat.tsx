import { useEffect, useRef, useState } from "react";
import { streamChat } from "../api";
import { Helpful } from "./Helpful";
import type { ChatTurn, StudentProfile, Verification } from "../types";

const TOOL_LABEL: Record<string, string> = {
  get_recommendations: "Checking courses against your profile…",
  search_sources: "Searching official sources…",
};

const STARTERS = [
  "Which course fits me best and why?",
  "How much should I budget for living in Dublin?",
  "What do I need for an Irish student visa?",
];

/** Shows whether every link in the answer came from the sources the agent actually retrieved. */
function Grounding({ v }: { v: Verification }) {
  if (v.unverified.length > 0)
    return <div className="grounding warn">⚠ Some links weren't among the official sources Compass retrieved for this answer. Double-check them.</div>;
  if (v.grounded)
    return <div className="grounding ok">✓ Based on {v.verified.length} official source{v.verified.length > 1 ? "s" : ""} retrieved for this answer</div>;
  if (!v.usedTools)
    return <div className="grounding info">General answer, not checked against official sources.</div>;
  return null;
}

function Linkified({ text }: { text: string }) {
  const parts = text.split(/(https?:\/\/[^\s)]+)/g);
  return <>{parts.map((p, i) => (p.startsWith("http") ? <a key={i} href={p} target="_blank" rel="noreferrer">{p}</a> : p))}</>;
}

export function Chat({ profile }: { profile: StudentProfile | null }) {
  const [turns, setTurns] = useState<ChatTurn[]>([]);
  const [draft, setDraft] = useState("");
  const [busy, setBusy] = useState(false);
  const [status, setStatus] = useState("");
  const [error, setError] = useState("");
  const abort = useRef<AbortController | null>(null);
  const end = useRef<HTMLDivElement>(null);

  // Braces matter: newer browsers return a Promise from scrollIntoView, which React would treat as a cleanup.
  useEffect(() => {
    end.current?.scrollIntoView({ block: "end" });
  }, [turns, status]);
  useEffect(() => () => {
    abort.current?.abort();
  }, []);

  const send = async (message: string) => {
    const text = message.trim();
    if (!text || busy) return;
    const history = turns;
    setTurns([...history, { role: "user", content: text }, { role: "assistant", content: "" }]);
    setDraft(""); setError(""); setBusy(true); setStatus("");
    abort.current = new AbortController();

    const appendToLast = (t: string) =>
      setTurns((prev) => {
        const next = prev.slice();
        const last = next[next.length - 1];
        next[next.length - 1] = { ...last, content: last.content + t };
        return next;
      });

    await streamChat(text, history, profile, {
      onToken: (t) => { setStatus(""); appendToLast(t); },
      onVerification: (v) => setTurns((prev) => {
        const next = prev.slice();
        const last = next[next.length - 1];
        if (last?.role === "assistant") next[next.length - 1] = { ...last, verification: v };
        return next;
      }),
      onTool: (name) => setStatus(TOOL_LABEL[name] ?? "Working…"),
      onError: (m) => setError(m),
      onDone: () => setStatus(""),
    }, abort.current.signal);

    setTurns((prev) => (prev[prev.length - 1]?.content === "" ? prev.slice(0, -1) : prev));
    setBusy(false); setStatus("");
  };

  return (
    <section className="panel chat" aria-label="Ask Compass">
      <h2>Ask Compass</h2>
      <div className="messages" aria-live="polite">
        {turns.length === 0 && (
          <div className="starters">
            <p>Answers come from your shortlist and official university and government pages, with links.</p>
            {STARTERS.map((s) => <button key={s} className="chip" onClick={() => send(s)}>{s}</button>)}
          </div>
        )}
        {turns.map((t, i) => (
          <div key={i} className={`msg ${t.role}`}>
            <Linkified text={t.content} />
            {t.verification && <Grounding v={t.verification} />}
            {t.verification && i === turns.length - 1 && !status && <Helpful key={i} context="chat" label="Did this answer help?" />}
          </div>
        ))}
        {status && <div className="status">{status}</div>}
        {error && <div className="error">{error}</div>}
        <div ref={end} />
      </div>
      <form onSubmit={(e) => { e.preventDefault(); send(draft); }}>
        <input value={draft} onChange={(e) => setDraft(e.target.value)} placeholder="Ask about courses, costs, visas…" maxLength={2000} disabled={busy} />
        <button className="primary" disabled={busy || !draft.trim()}>Send</button>
      </form>
      <small className="disclaimer">Not legal or immigration advice. Always confirm on the official site.</small>
    </section>
  );
}
