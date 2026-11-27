import { useCallback, useEffect, useRef, useState } from "react";

/** useState that survives reloads (localStorage). Falls back to memory in private mode. */
export function usePersistentState<T>(key: string, initial: T): [T, (v: T | ((prev: T) => T)) => void] {
  const [value, setValue] = useState<T>(() => {
    try {
      const raw = localStorage.getItem(key);
      return raw === null ? initial : (JSON.parse(raw) as T);
    } catch {
      return initial;
    }
  });
  useEffect(() => {
    try { localStorage.setItem(key, JSON.stringify(value)); } catch { /* ignore */ }
  }, [key, value]);
  return [value, setValue];
}

const clamp = (v: number, min: number, max: number) => Math.min(max, Math.max(min, v));

interface HandleProps {
  /** Called with the pointer movement since the drag started (px). */
  onDrag: (dx: number, dy: number) => void;
  onStart: () => void;
  label: string;
  className?: string;
  orientation?: "vertical" | "horizontal" | "corner";
  /** Keyboard resizing: arrow keys call onStep with ±16. */
  onStep?: (delta: number) => void;
}

/** A drag handle for resizing panels with mouse, touch or keyboard. */
export function DragHandle({ onDrag, onStart, label, className = "", orientation = "vertical", onStep }: HandleProps) {
  const origin = useRef<{ x: number; y: number } | null>(null);

  const down = (e: React.PointerEvent) => {
    e.preventDefault();
    (e.target as HTMLElement).setPointerCapture(e.pointerId);
    origin.current = { x: e.clientX, y: e.clientY };
    onStart();
    document.body.classList.add("resizing");
  };
  const move = (e: React.PointerEvent) => {
    if (origin.current) onDrag(e.clientX - origin.current.x, e.clientY - origin.current.y);
  };
  const up = () => {
    origin.current = null;
    document.body.classList.remove("resizing");
  };

  return (
    <div
      role="separator"
      aria-orientation={orientation === "horizontal" ? "horizontal" : "vertical"}
      aria-label={label}
      tabIndex={0}
      className={`drag-handle ${orientation} ${className}`}
      onPointerDown={down}
      onPointerMove={move}
      onPointerUp={up}
      onPointerCancel={up}
      onKeyDown={(e) => {
        if (!onStep) return;
        if (e.key === "ArrowLeft" || e.key === "ArrowUp") { e.preventDefault(); onStep(-16); }
        if (e.key === "ArrowRight" || e.key === "ArrowDown") { e.preventDefault(); onStep(16); }
      }}
    />
  );
}

/** A width that the user can drag between min and max, remembered across visits. */
export function useResizable(key: string, initial: number, min: number, max: number) {
  const [size, setSize] = usePersistentState(key, initial);
  const start = useRef(size);
  const onStart = useCallback(() => { start.current = size; }, [size]);
  const resizeBy = useCallback((delta: number) => setSize(clamp(start.current + delta, min, max)), [setSize, min, max]);
  const step = useCallback((delta: number) => setSize((s) => clamp(s + delta, min, max)), [setSize, min, max]);
  return { size: clamp(size, min, max), onStart, resizeBy, step };
}
