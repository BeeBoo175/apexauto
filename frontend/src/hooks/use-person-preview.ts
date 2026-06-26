"use client";

import { useEffect, useState } from "react";
import { parsePerson, type Person } from "@/lib/account/contracts";
import { privateApiFetch } from "@/lib/api-browser";
import { sessionChanges } from "@/lib/auth/browser-session";

const HOVER_INTENT_MS = 250;
const CACHE_LIFETIME_MS = 60_000;

const reads = new Map<string, { expiresAt: number; read: Promise<Person | null> }>();

// What a person may see differs per signed-in account, so a session change forgets every read.
sessionChanges?.addEventListener("message", () => reads.clear());

function readPerson(id: string) {
  const cached = reads.get(id);
  if (cached && cached.expiresAt > Date.now()) {
    return cached.read;
  }
  const read = privateApiFetch(`/api/people/${id}`, parsePerson)
    .then((result) => (result.status === 200 && result.data?.userId === id ? result.data : null))
    .catch(() => null);
  reads.set(id, { expiresAt: Date.now() + CACHE_LIFETIME_MS, read });
  void read.then((person) => {
    if (!person) {
      reads.delete(id);
    }
  });
  return read;
}

// Contact details load only after the pointer or focus rests on an avatar, and are shared between
// every avatar of the same person for a short while.
export function usePersonPreview(id: string, open: boolean) {
  const [loaded, setLoaded] = useState<{ id: string; person: Person | null } | null>(null);

  useEffect(() => {
    if (!open) {
      return;
    }
    let current = true;
    const timer = window.setTimeout(() => {
      void readPerson(id).then((person) => {
        if (current) {
          setLoaded({ id, person });
        }
      });
    }, HOVER_INTENT_MS);
    return () => {
      current = false;
      window.clearTimeout(timer);
    };
  }, [id, open]);

  return loaded?.id === id ? loaded.person : null;
}
