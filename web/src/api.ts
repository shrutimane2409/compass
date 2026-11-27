import { createSseParser } from "./sse";
import type { Application, ChatTurn, Course, ExchangeRate, Me, Priority, RecommendationResponse, ScholarshipMatch, StudentProfile, User, Verification } from "./types";

// Empty in development (Vite proxies /api). Set VITE_API_BASE when the API is on another host.
const API_BASE = (import.meta.env.VITE_API_BASE ?? "").replace(/\/$/, "");

// Anonymous id for this browser tab, used only to count distinct sessions. Never tied to a person.
let sessionId = "";
function session(): string {
  if (sessionId) return sessionId;
  try {
    sessionId = sessionStorage.getItem("compass.sid") ?? "";
    if (!sessionId) { sessionId = crypto.randomUUID(); sessionStorage.setItem("compass.sid", sessionId); }
  } catch {
    sessionId ||= crypto.randomUUID?.() ?? "";
  }
  return sessionId;
}
// Session token from Google sign-in (bearer, because the API is on another site). Empty for guests.
let authToken = "";
export const setAuthToken = (token: string) => { authToken = token; };
const sid = (): Record<string, string> =>
  authToken ? { "X-Session-Id": session(), Authorization: `Bearer ${authToken}` } : { "X-Session-Id": session() };

export class ApiError extends Error {
  constructor(message: string, public status: number) {
    super(message);
  }
}

async function problemMessage(res: Response): Promise<string> {
  try {
    const body = await res.json();
    if (body.errors) return Object.values(body.errors).flat().join(" ");
    return body.detail ?? body.title ?? `Request failed (${res.status})`;
  } catch {
    return `Request failed (${res.status})`;
  }
}

export async function getRecommendations(
  profile: StudentProfile, priority: Priority, signal?: AbortSignal,
): Promise<RecommendationResponse> {
  const res = await fetch(`${API_BASE}/api/recommendations`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...sid() },
    body: JSON.stringify({ profile, priority }),
    signal,
  });
  if (res.status === 429) throw new ApiError("Too many requests. Wait a moment and try again.", 429);
  if (!res.ok) throw new ApiError(await problemMessage(res), res.status);
  return res.json();
}

export async function getExchangeRate(currency = "INR"): Promise<ExchangeRate | null> {
  try {
    const res = await fetch(`${API_BASE}/api/fx/${currency}`);
    return res.ok ? await res.json() : null;
  } catch {
    return null;
  }
}

export type FeedbackField = "fee" | "entry-requirements" | "english" | "deadline" | "living-costs" | "other";

export async function sendFeedback(courseId: number, field: FeedbackField, message: string): Promise<void> {
  const res = await fetch(`${API_BASE}/api/feedback`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...sid() },
    body: JSON.stringify({ courseId, field, message, pageUrl: location.href }),
  });
  if (res.status === 429) throw new ApiError("Too many reports. Try again later.", 429);
  if (!res.ok) throw new ApiError(await problemMessage(res), res.status);
}

export interface ChatHandlers {
  onVerification?: (v: Verification) => void;
  onToken: (text: string) => void;
  onTool: (name: string) => void;
  onError: (message: string) => void;
  onDone: () => void;
}

export async function streamChat(
  message: string, history: ChatTurn[], profile: StudentProfile | null,
  handlers: ChatHandlers, signal?: AbortSignal,
): Promise<void> {
  let res: Response;
  try {
    res = await fetch(`${API_BASE}/api/chat`, {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "text/event-stream", ...sid() },
      body: JSON.stringify({ message, history: history.slice(-12), profile }),
      signal,
    });
  } catch {
    handlers.onError("Can't reach Compass right now.");
    return;
  }
  if (!res.ok || !res.body) {
    handlers.onError(res.status === 429 ? "You're sending messages quickly. Try again in a minute." : await problemMessage(res));
    return;
  }

  const reader = res.body.pipeThrough(new TextDecoderStream()).getReader();
  const parse = createSseParser();
  for (;;) {
    const { value, done } = await reader.read();
    if (done) break;
    for (const ev of parse(value)) {
      const data = JSON.parse(ev.data || "{}");
      if (ev.event === "token") handlers.onToken(data.text ?? "");
      else if (ev.event === "tool") handlers.onTool(data.name ?? "");
      else if (ev.event === "error") handlers.onError(data.message ?? "Something went wrong.");
      else if (ev.event === "verification") handlers.onVerification?.(data as Verification);
      else if (ev.event === "done") handlers.onDone();
    }
  }
}

/** Fire-and-forget "was this helpful?" signal. Failures are ignored on purpose. */
export function sendHelpful(helpful: boolean, context: "shortlist" | "chat"): void {
  fetch(`${API_BASE}/api/events/helpful`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...sid() },
    body: JSON.stringify({ helpful, context }),
    keepalive: true,
  }).catch(() => {});
}

async function call<T>(method: string, path: string, body?: unknown): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    method,
    headers: { "Content-Type": "application/json", ...sid() },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (res.status === 429) throw new ApiError("Too many requests. Wait a moment and try again.", 429);
  if (!res.ok) throw new ApiError(await problemMessage(res), res.status);
  return (res.status === 204 ? undefined : await res.json()) as T;
}

export const getAuthConfig = () =>
  call<{ enabled: boolean; googleClientId: string | null }>("GET", "/api/auth/config").catch(() => ({ enabled: false, googleClientId: null }));
export const signInWithGoogle = (credential: string) =>
  call<{ token: string; expiresAt: string; user: User }>("POST", "/api/auth/google", { credential });
export const getMe = () => call<Me>("GET", "/api/me");
export const saveProfile = (profile: StudentProfile, priority: Priority) => call<void>("PUT", "/api/me/profile", { profile, priority });
export const saveCourse = (courseId: number, note?: string) => call<void>("PUT", `/api/me/saved/${courseId}`, { note: note ?? null });
export const unsaveCourse = (courseId: number) => call<void>("DELETE", `/api/me/saved/${courseId}`);
export const saveApplication = (a: Pick<Application, "courseId" | "status" | "documents" | "notes">) =>
  call<void>("PUT", `/api/me/applications/${a.courseId}`, { status: a.status, documents: a.documents, notes: a.notes });
export const deleteApplication = (courseId: number) => call<void>("DELETE", `/api/me/applications/${courseId}`);
export const setChecklistItem = (itemId: string, done: boolean) => call<void>("PUT", `/api/me/checklist/${itemId}`, { done });
export const deleteAccount = () => call<void>("DELETE", "/api/me");
export const getCourses = () => call<Course[]>("GET", "/api/courses");
export const matchScholarships = (profile: StudentProfile, courseIds: number[] | null) =>
  call<ScholarshipMatch[]>("POST", "/api/scholarships/match", { profile, courseIds });

/** Downloads everything Compass stores about the signed-in student as a JSON file. */
export async function exportMyData(): Promise<void> {
  const res = await fetch(`${API_BASE}/api/me/export`, { headers: sid() });
  if (!res.ok) throw new ApiError(await problemMessage(res), res.status);
  const url = URL.createObjectURL(await res.blob());
  const a = document.createElement("a");
  a.href = url;
  a.download = "compass-my-data.json";
  a.click();
  URL.revokeObjectURL(url);
}

/** Wakes the free-tier AI service early. Failures don't matter. */
export function warmup(): void {
  fetch(`${API_BASE}/api/warmup`, { method: "POST" }).catch(() => {});
}
