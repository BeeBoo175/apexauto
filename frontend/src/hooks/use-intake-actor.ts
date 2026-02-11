"use client";
import { useEffect, useState } from "react";
import { flushSync } from "react-dom";
import { sessionChanges } from "@/lib/auth/browser-session";
import { actorIdentity, parseActor, type CurrentActor } from "@/lib/auth/actor";

export function useIntakeActor(expected?: CurrentActor) {
  const [active, setActive] = useState(true);
  const [ready, setReady] = useState(false);
  const identity = expected ? actorIdentity(expected) : "";
  useEffect(() => {
    queueMicrotask(() => setReady(true));
    function invalidate() {
      // Unmount private drafts and their unload guards before replacing the actor's document.
      flushSync(() => setActive(false));
      window.location.reload();
    }
    function restore(event: PageTransitionEvent) {
      if (event.persisted) {
        invalidate();
      }
    }
    function compare(event: Event) {
      const actor = parseActor((event as CustomEvent<unknown>).detail);
      if (actor && identity && actorIdentity(actor) !== identity) {
        invalidate();
      }
    }
    sessionChanges?.addEventListener("message", invalidate);
    window.addEventListener("apexauto-session-invalid", invalidate);
    window.addEventListener("apexauto-intake-actor", compare);
    window.addEventListener("pageshow", restore);
    return () => {
      sessionChanges?.removeEventListener("message", invalidate);
      window.removeEventListener("apexauto-session-invalid", invalidate);
      window.removeEventListener("apexauto-intake-actor", compare);
      window.removeEventListener("pageshow", restore);
    };
  }, [identity]);
  return { active, ready };
}
