"use client";

import { prefersReducedMotion } from "@/lib/motion";
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useId,
  useLayoutEffect,
  useRef,
  useState,
  type ReactNode,
} from "react";
import styles from "./LoadingProgress.module.css";

const ProgressContext = createContext<((id: string, label: string) => () => void) | null>(null);
const ProgressReadyContext = createContext<(() => void) | null>(null);

function synchronizeLayer(bar: HTMLElement) {
  if (typeof bar.showPopover !== "function") {
    return;
  }
  if (bar.matches(":popover-open")) {
    bar.hidePopover();
  }
  if (bar.hidden || !document.querySelector("dialog[open]")) {
    bar.removeAttribute("popover");
    return;
  }
  bar.setAttribute("popover", "manual");
  bar.showPopover();
}

// Shows an idle or finishing bar and begins its opening stroke at once. Returns that stroke, or null when the bar is
// already running and the provider's effect owns it.
function startStroke(bar: HTMLElement) {
  const track = bar.firstElementChild;
  if (!(track instanceof HTMLElement) || !(bar.hidden || bar.dataset.phase === "completing")) {
    return null;
  }
  bar.getAnimations().forEach((animation) => animation.cancel());
  track.getAnimations().forEach((animation) => animation.cancel());
  track.style.transform = "scaleX(0.04)";
  bar.hidden = false;
  bar.dataset.phase = "pending";
  synchronizeLayer(bar);
  const reduced = prefersReducedMotion();
  return track.animate([{ transform: "scaleX(0.04)" }, { transform: "scaleX(0.8)" }], {
    duration: reduced ? 1 : 1100,
    easing: "cubic-bezier(0.16, 1, 0.3, 1)",
    fill: "forwards",
  });
}

// `initialPending` keeps the bar running until the first entry read reports ready (the dashboard). Pages that
// render their content on the server leave it false, so the bar only appears while a registered read runs.
export function LoadingProgressProvider({
  children,
  initialLabel = "Opening workspace",
  initialPending: startsPending = true,
}: {
  children: ReactNode;
  initialLabel?: string;
  initialPending?: boolean;
}) {
  const element = useRef<HTMLSpanElement>(null);
  const barId = useId();
  const started = useRef(0);
  const opening = useRef<Animation | null>(null);
  const [initialPending, setInitialPending] = useState(startsPending);
  const [activities, setActivities] = useState(() => new Map<string, string>());
  const active = initialPending || activities.size > 0;
  const ready = useCallback(() => setInitialPending(false), []);
  const register = useCallback(
    (id: string, label: string) => {
      // Show the bar in the same frame as the interaction that started the work, instead of after the re-render the
      // registration causes; the effect below then takes over the same stroke.
      // Found by this provider's own id, so a bar nested under another provider never starts the outer one.
      const bar = document.querySelector<HTMLElement>(
        `[data-loading-progress="${CSS.escape(barId)}"]`,
      );
      const stroke = bar ? startStroke(bar) : null;
      if (stroke) {
        started.current = performance.now();
        opening.current = stroke;
      }
      setActivities((current) => new Map(current).set(id, label));
      return () => {
        setActivities((current) => {
          const next = new Map(current);
          next.delete(id);
          return next;
        });
      };
    },
    [barId],
  );

  useEffect(() => {
    const bar = element.current;
    const child = bar?.firstElementChild;
    if (!bar || !(child instanceof HTMLElement)) {
      return;
    }
    const track = child;
    if (!bar.dataset.phase) {
      const elapsed = track.getAnimations()[0]?.currentTime;
      started.current = performance.now() - (typeof elapsed === "number" ? elapsed : 0);
      track.style.transform = getComputedStyle(track).transform;
      bar.dataset.phase = "pending";
    }
    const reduced = prefersReducedMotion();
    let frame = 0;
    let paintFrame = 0;
    let completionTimer = 0;
    let disposed = false;
    let fill: Animation | null = null;
    let fade: Animation | null = null;
    const updateLayer = () => synchronizeLayer(bar);
    document.addEventListener("apexauto-dialog-change", updateLayer);

    function animateFill(target: number, duration: number) {
      fill = track.animate(
        [{ transform: getComputedStyle(track).transform }, { transform: `scaleX(${target})` }],
        {
          duration: reduced ? 1 : duration,
          easing: "cubic-bezier(0.16, 1, 0.3, 1)",
          fill: "forwards",
        },
      );
    }

    async function complete() {
      if (!bar) {
        return;
      }
      bar.dataset.phase = "completing";
      animateFill(1, 180);
      await fill?.finished.catch(() => {});
      if (disposed) {
        return;
      }
      fade = bar.animate([{ opacity: 1 }, { opacity: 0 }], { duration: 180, fill: "forwards" });
      await fade.finished.catch(() => {});
      if (!disposed) {
        bar.hidden = true;
        bar.dataset.phase = "hidden";
        track.style.transform = "scaleX(0.04)";
        started.current = 0;
        synchronizeLayer(bar);
      }
    }

    function afterContentPaint() {
      frame = requestAnimationFrame(() => {
        paintFrame = requestAnimationFrame(() => void complete());
      });
    }

    // Continue the stroke the registration already started rather than restarting or abandoning it.
    if (opening.current) {
      track.style.transform = getComputedStyle(track).transform;
      opening.current.cancel();
      opening.current = null;
    }
    if (active) {
      if (bar.dataset.phase === "completing") {
        track.style.transform = "scaleX(0.04)";
        started.current = 0;
      }
      started.current ||= performance.now();
      bar.hidden = false;
      synchronizeLayer(bar);
      bar.dataset.phase = "pending";
      const scale = new DOMMatrixReadOnly(getComputedStyle(track).transform).a;
      animateFill(Math.max(0.8, scale), 1100);
    } else if (!bar.hidden) {
      // Ready controls stay usable while a fast read finishes its opening stroke.
      const remaining = reduced ? 0 : Math.max(0, 300 - (performance.now() - started.current));
      if (remaining > 0) {
        animateFill(0.8, remaining);
      }
      completionTimer = window.setTimeout(afterContentPaint, remaining);
    }

    return () => {
      disposed = true;
      clearTimeout(completionTimer);
      document.removeEventListener("apexauto-dialog-change", updateLayer);
      cancelAnimationFrame(frame);
      cancelAnimationFrame(paintFrame);
      if (!bar.hidden) {
        track.style.transform = getComputedStyle(track).transform;
      }
      fill?.cancel();
      fade?.cancel();
    };
  }, [active]);

  const label = Array.from(activities.values()).pop() ?? initialLabel;
  return (
    <ProgressContext.Provider value={register}>
      <span
        ref={element}
        className={styles.progress}
        role="progressbar"
        aria-label={label}
        data-loading-progress={barId}
        hidden={!startsPending}
      >
        <span className={styles.track} />
      </span>
      <ProgressReadyContext.Provider value={ready}>{children}</ProgressReadyContext.Provider>
    </ProgressContext.Provider>
  );
}

export function useLoadingProgressReady(active = true) {
  const ready = useContext(ProgressReadyContext);
  useEffect(() => {
    if (active) {
      ready?.();
    }
  }, [active, ready]);
}

export function LoadingProgressReady() {
  useLoadingProgressReady();
  return null;
}

// Registers in the layout phase, so work started by a click shows the bar before the browser paints that click.
export function useLoadingProgress(label: string, active = true) {
  const register = useContext(ProgressContext);
  const id = useId();
  useLayoutEffect(() => {
    if (active) {
      return register?.(id, label);
    }
  }, [register, id, label, active]);
}

export function LoadingProgress({ label, active = true }: { label: string; active?: boolean }) {
  useLoadingProgress(label, active);
  return null;
}
