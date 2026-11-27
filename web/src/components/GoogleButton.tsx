import { useEffect, useRef } from "react";
import { useAccount, type GoogleApi } from "../account";

const SCRIPT = "https://accounts.google.com/gsi/client";

function loadGoogle(): Promise<GoogleApi> {
  const w = window as unknown as { google?: GoogleApi };
  if (w.google) return Promise.resolve(w.google);
  return new Promise((resolve, reject) => {
    const existing = document.querySelector<HTMLScriptElement>(`script[src="${SCRIPT}"]`);
    const s = existing ?? Object.assign(document.createElement("script"), { src: SCRIPT, async: true, defer: true });
    s.addEventListener("load", () => (w.google ? resolve(w.google) : reject(new Error("Google sign-in unavailable"))));
    s.addEventListener("error", () => reject(new Error("Google sign-in unavailable")));
    if (!existing) document.head.appendChild(s);
  });
}

/** Google's own "Sign in with Google" button. Renders nothing when sign-in isn't configured. */
export function GoogleButton({ width = 240 }: { width?: number }) {
  const { enabled, googleClientId, signIn } = useAccount();
  const el = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!enabled || !googleClientId || !el.current) return;
    let cancelled = false;
    loadGoogle().then((g) => {
      if (cancelled || !el.current) return;
      g.accounts.id.initialize({ client_id: googleClientId, callback: (r) => signIn(r.credential) });
      g.accounts.id.renderButton(el.current, { theme: "outline", size: "large", shape: "pill", text: "signin_with", width });
    }).catch(() => { /* blocked by an extension or offline: guest mode still works */ });
    return () => { cancelled = true; };
  }, [enabled, googleClientId, signIn, width]);

  if (!enabled) return null;
  return <div ref={el} className="google-btn" />;
}
