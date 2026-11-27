import { useRef } from "react";
import { DragHandle, usePersistentState } from "../layout";
import type { StudentProfile } from "../types";
import { Chat } from "./Chat";

/**
 * "Ask Compass" as a floating dock: minimised to a button by default, opens over the page,
 * and can be resized by dragging its top-left corner (or the left edge). Size and open state are remembered.
 */
export function ChatDock({ profile }: { profile: StudentProfile | null }) {
  const [open, setOpen] = usePersistentState("compass.chat.open", false);
  const [size, setSize] = usePersistentState("compass.chat.size", { w: 400, h: 560 });
  const start = useRef(size);
  const clampSize = (w: number, h: number) => ({
    w: Math.min(Math.max(w, 320), Math.min(760, window.innerWidth - 32)),
    h: Math.min(Math.max(h, 360), window.innerHeight - 96),
  });

  const s = clampSize(size.w, size.h);
  // The chat stays mounted while minimised, so the conversation isn't lost.
  return (
    <>
      {!open && (
      <button type="button" className="chat-launcher primary" onClick={() => setOpen(true)} aria-expanded={false}>
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
          <path d="M21 12a8 8 0 0 1-11.6 7.1L4 20l1-4.6A8 8 0 1 1 21 12z" />
        </svg>
        Ask Compass
      </button>
      )}
    <div className="chat-dock" style={{ width: s.w, height: s.h }} hidden={!open}>
      <DragHandle orientation="corner" className="dock-corner" label="Resize chat"
                  onStart={() => { start.current = s; }}
                  onDrag={(dx, dy) => setSize(clampSize(start.current.w - dx, start.current.h - dy))} />
      <DragHandle orientation="vertical" className="dock-edge" label="Resize chat width"
                  onStart={() => { start.current = s; }}
                  onDrag={(dx) => setSize(clampSize(start.current.w - dx, start.current.h))}
                  onStep={(d) => setSize(clampSize(s.w - d, s.h))} />
      <button type="button" className="icon-btn dock-min" onClick={() => setOpen(false)} aria-label="Minimise chat" title="Minimise">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true"><path d="M5 12h14" /></svg>
      </button>
      <Chat profile={profile} />
    </div>
    </>
  );
}
