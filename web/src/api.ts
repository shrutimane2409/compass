import { createSseParser } from "./sse";
import type { ChatTurn, RecommendationResponse, StudentProfile } from "./types";

// Empty in development (Vite proxies /api). Set VITE_API_BASE when the API is on another host.
const API_BASE = (import.meta.env.VITE_API_BASE ?? "").replace(/\/$/, "");

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

export async function getRecommendations(profile: StudentProfile, signal?: AbortSignal): Promise<RecommendationResponse> {
  const res = await fetch(`${API_BASE}/api/recommendations`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ profile }),
    signal,
  });
  if (res.status === 429) throw new ApiError("Too many requests. Wait a moment and try again.", 429);
  if (!res.ok) throw new ApiError(await problemMessage(res), res.status);
  return res.json();
}

export interface ChatHandlers {
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
      headers: { "Content-Type": "application/json", Accept: "text/event-stream" },
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
      else if (ev.event === "done") handlers.onDone();
    }
  }
}
