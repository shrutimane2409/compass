import { useState } from "react";
import { useAccount } from "../account";
import { CHECKLIST } from "../checklist";

export function Prearrival() {
  const account = useAccount();
  const [local, setLocal] = useState<string[]>([]);
  const done = account.me ? account.me.checklistDone : local;
  const toggle = (id: string) =>
    account.me ? account.toggleChecklist(id) : setLocal((l) => (l.includes(id) ? l.filter((x) => x !== id) : [...l, id]));
  const count = CHECKLIST.filter((i) => done.includes(i.id)).length;

  return (
    <div className="page">
      <h1>Pre-arrival checklist</h1>
      <p className="muted">From offer letter to your first month in Ireland. {account.me ? "Your progress is saved." : "Sign in to keep your progress."}</p>
      <div className="progress-line"><b>{count} of {CHECKLIST.length} done</b><i className="bar"><i style={{ width: `${(count / CHECKLIST.length) * 100}%` }} /></i></div>
      <ol className="panel checklist">
        {CHECKLIST.map((item) => (
          <li key={item.id} className={done.includes(item.id) ? "done" : ""}>
            <label>
              <input type="checkbox" checked={done.includes(item.id)} onChange={() => toggle(item.id)} />
              <span className="grow">
                <b>{item.label}</b>
                <span className="muted small">{item.detail}{item.url && <> <a href={item.url} target="_blank" rel="noreferrer">{item.source} ↗</a></>}</span>
              </span>
              <span className="when">{item.when}</span>
            </label>
          </li>
        ))}
      </ol>
    </div>
  );
}
