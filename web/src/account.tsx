import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import * as api from "./api";
import type { Application, AppStatus, Me, Priority, StudentProfile } from "./types";

const TOKEN_KEY = "compass.token";

interface Account {
  /** Sign-in is configured on the server (a Google client id exists). */
  enabled: boolean;
  googleClientId: string | null;
  me: Me | null;
  loading: boolean;
  error: string;
  signIn: (credential: string) => Promise<void>;
  signOut: () => void;
  isSaved: (courseId: number) => boolean;
  toggleSaved: (courseId: number) => Promise<void>;
  application: (courseId: number) => Application | undefined;
  track: (courseId: number, status?: AppStatus) => Promise<void>;
  updateApplication: (a: Application) => Promise<void>;
  untrack: (courseId: number) => Promise<void>;
  toggleChecklist: (itemId: string) => Promise<void>;
  saveProfile: (profile: StudentProfile, priority: Priority) => Promise<void>;
  deleteAccount: () => Promise<void>;
}

const Ctx = createContext<Account | null>(null);

function readToken(): string {
  try { return localStorage.getItem(TOKEN_KEY) ?? ""; } catch { return ""; }
}
function writeToken(token: string) {
  try { token ? localStorage.setItem(TOKEN_KEY, token) : localStorage.removeItem(TOKEN_KEY); } catch { /* private mode */ }
}

/**
 * Optional account state. Every change is applied to local state first (so the UI responds instantly)
 * and rolled back if the server rejects it.
 */
export function AccountProvider({ children }: { children: ReactNode }) {
  const [config, setConfig] = useState<{ enabled: boolean; googleClientId: string | null }>({ enabled: false, googleClientId: null });
  const [me, setMe] = useState<Me | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const meRef = useRef(me);
  meRef.current = me;

  const signOut = useCallback(() => {
    writeToken("");
    api.setAuthToken("");
    setMe(null);
    (window as unknown as { google?: GoogleApi }).google?.accounts.id.disableAutoSelect();
  }, []);

  useEffect(() => {
    api.getAuthConfig().then(setConfig);
    const token = readToken();
    if (!token) { setLoading(false); return; }
    api.setAuthToken(token);
    api.getMe()
      .then(setMe)
      .catch((e) => { if ((e as api.ApiError).status === 401) signOut(); })
      .finally(() => setLoading(false));
  }, [signOut]);

  const signIn = useCallback(async (credential: string) => {
    setError("");
    try {
      const res = await api.signInWithGoogle(credential);
      writeToken(res.token);
      api.setAuthToken(res.token);
      setMe(await api.getMe());
    } catch (e) {
      setError((e as Error).message);
    }
  }, []);

  /** Optimistic update: apply locally, call the server, restore the previous state on failure. */
  const mutate = useCallback(async (change: (m: Me) => Me, request: () => Promise<void>) => {
    const before = meRef.current;
    setMe((m) => (m ? change(m) : m));
    try {
      await request();
      setError("");
    } catch (e) {
      setMe(before);
      setError((e as Error).message);
      if ((e as api.ApiError).status === 401) signOut();
    }
  }, [signOut]);

  const value = useMemo<Account>(() => {
    const isSaved = (id: number) => !!me?.saved.some((s) => s.courseId === id);
    const application = (id: number) => me?.applications.find((a) => a.courseId === id);
    const upsertApp = (a: Application) => (m: Me) => ({
      ...m, applications: [a, ...m.applications.filter((x) => x.courseId !== a.courseId)],
    });
    return {
      enabled: config.enabled,
      googleClientId: config.googleClientId,
      me, loading, error, signIn, signOut, isSaved, application,
      toggleSaved: (id) => isSaved(id)
        ? mutate((m) => ({ ...m, saved: m.saved.filter((s) => s.courseId !== id) }), () => api.unsaveCourse(id))
        : mutate((m) => ({ ...m, saved: [{ courseId: id, note: null, createdAt: new Date().toISOString() }, ...m.saved] }), () => api.saveCourse(id)),
      track: (id, status = "researching") => {
        const a: Application = application(id) ?? { courseId: id, status, documents: {}, notes: null, updatedAt: new Date().toISOString() };
        return mutate(upsertApp(a), () => api.saveApplication(a));
      },
      updateApplication: (a) => mutate(upsertApp({ ...a, updatedAt: new Date().toISOString() }), () => api.saveApplication(a)),
      untrack: (id) => mutate((m) => ({ ...m, applications: m.applications.filter((x) => x.courseId !== id) }), () => api.deleteApplication(id)),
      toggleChecklist: (itemId) => {
        const done = !me?.checklistDone.includes(itemId);
        return mutate(
          (m) => ({ ...m, checklistDone: done ? [...m.checklistDone, itemId] : m.checklistDone.filter((x) => x !== itemId) }),
          () => api.setChecklistItem(itemId, done));
      },
      saveProfile: (profile, priority) => mutate((m) => ({ ...m, profile, priority }), () => api.saveProfile(profile, priority)),
      deleteAccount: async () => {
        await api.deleteAccount();
        signOut();
      },
    };
  }, [config, me, loading, error, signIn, signOut, mutate]);

  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

export function useAccount(): Account {
  const ctx = useContext(Ctx);
  if (!ctx) throw new Error("useAccount must be used inside AccountProvider");
  return ctx;
}

/** The small part of Google Identity Services that Compass uses. */
export interface GoogleApi {
  accounts: {
    id: {
      initialize: (o: { client_id: string; callback: (r: { credential: string }) => void; auto_select?: boolean }) => void;
      renderButton: (el: HTMLElement, o: Record<string, string | number>) => void;
      disableAutoSelect: () => void;
    };
  };
}
