"use client";

import { useEffect, useState, useSyncExternalStore } from "react";
import { parsePerson, type Person } from "@/lib/account/contracts";
import { privateApiFetch } from "@/lib/api-browser";
import { sessionChanges } from "@/lib/auth/browser-session";

const HOVER_INTENT_MS = 250;

// A person's profile rarely changes, so it is read once per signed-in session and shared by every avatar.
// `known` holds the finished reads, so a later hover or dialog renders the person on its first frame.
const reads = new Map<string, Promise<Person | null>>();
const known = new Map<string, Person>();
const listeners = new Set<() => void>();

const subscribe = (listener: () => void) => {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
};

// What a person may see differs per signed-in account, so a session change forgets every read.
sessionChanges?.addEventListener("message", () => {
  reads.clear();
  known.clear();
  listeners.forEach((listener) => listener());
});

function readPerson(id: string) {
  const cached = reads.get(id);
  if (cached) {
    return cached;
  }
  const read = privateApiFetch(`/api/people/${id}`, parsePerson)
    .then((result) => (result.status === 200 && result.data?.userId === id ? result.data : null))
    .catch(() => null);
  reads.set(id, read);
  void read.then((person) => {
    if (person) {
      known.set(id, person);
      listeners.forEach((listener) => listener());
    } else {
      // A failed read is retried the next time someone asks.
      reads.delete(id);
    }
  });
  return read;
}

// Contact details are read only once the pointer or focus rests on an avatar (or a dialog opens), and only the
// first time: after that every avatar and dialog of the same person shows them immediately.
export function usePersonPreview(id: string, open: boolean, delayMs = HOVER_INTENT_MS) {
  const [loaded, setLoaded] = useState<{ id: string; person: Person | null } | null>(null);
  // Re-renders when another avatar of the same person finishes the shared read.
  const cached = useSyncExternalStore(
    subscribe,
    () => known.get(id),
    () => undefined,
  );

  useEffect(() => {
    if (!open || known.has(id)) {
      return;
    }
    let current = true;
    const timer = window.setTimeout(() => {
      void readPerson(id).then((person) => {
        if (current) {
          setLoaded({ id, person });
        }
      });
    }, delayMs);
    return () => {
      current = false;
      window.clearTimeout(timer);
    };
  }, [id, open, delayMs]);

  if (cached) {
    return { person: cached, settled: true };
  }

  const settled = loaded?.id === id;
  return { person: settled ? loaded.person : null, settled };
}
