import { useState } from "react";
import type { Shared } from "../App";
import { useAccount } from "../account";
import { exportMyData } from "../api";
import { ProfileForm } from "../components/ProfileForm";
import { SignInPrompt } from "../components/SignInPrompt";
import { toProfile } from "../profile";
import type { Priority } from "../types";
import { PRIORITY_LABEL } from "./Explore";

export function Profile({ form, setForm, submit, priority, setPriority, fx }: Shared) {
  const account = useAccount();
  const [status, setStatus] = useState("");
  const [confirming, setConfirming] = useState(false);

  if (!account.me) {
    return (
      <div className="page narrow">
        <h1>Profile &amp; preferences</h1>
        <SignInPrompt title="Save your profile"
                      text="Sign in with Google and Compass remembers your grades, English score, budget and interests, so your matches are ready every time you come back. Only what you save is stored, and you can delete it any time." />
      </div>
    );
  }

  const save = async () => {
    await account.saveProfile(toProfile(form), priority);
    submit();
    setStatus("Saved. Your matches are updated.");
  };

  return (
    <div className="page narrow">
      <h1>Profile &amp; preferences</h1>
      <p className="muted">Signed in as {account.me.user.email}. Change anything and save: your matches and scholarships update.</p>
      <label className="field inline">
        <span>What matters most when ranking</span>
        <select value={priority} onChange={(e) => setPriority(e.target.value as Priority)}>
          {(Object.keys(PRIORITY_LABEL) as Priority[]).map((p) => <option key={p} value={p}>{PRIORITY_LABEL[p]}</option>)}
        </select>
      </label>
      <ProfileForm value={form} onChange={(f) => { setForm(f); setStatus(""); }} onSubmit={save} submitted fx={fx} submitLabel="Save profile" />
      {status && <p className="ok small" role="status">{status}</p>}

      <section className="panel">
        <h2>Your data</h2>
        <p className="muted small">Compass stores your Google name and email, the profile above, saved courses, applications and checklist progress. Nothing is shared or sold.</p>
        <div className="row gap">
          <button type="button" className="secondary" onClick={() => exportMyData().catch((e) => setStatus((e as Error).message))}>Download my data</button>
          {confirming ? (
            <>
              <span className="small">Delete your account and everything saved? This can't be undone.</span>
              <button type="button" className="danger" onClick={() => account.deleteAccount().catch((e) => setStatus((e as Error).message))}>Yes, delete</button>
              <button type="button" className="secondary" onClick={() => setConfirming(false)}>Cancel</button>
            </>
          ) : (
            <button type="button" className="secondary danger-outline" onClick={() => setConfirming(true)}>Delete account</button>
          )}
        </div>
      </section>
    </div>
  );
}
