"use client";

// One channel per document notifies other tabs without sharing credentials or account data.
export const sessionChanges =
  typeof window === "undefined" ? null : new BroadcastChannel("apexauto-session");
