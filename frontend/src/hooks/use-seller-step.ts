"use client";

import { useEffect, useState, useSyncExternalStore } from "react";
import { writeAddress } from "@/lib/browser-url";
import { SELLER_STEP_KEY, sellerStepFromToken, sellerStepToken } from "@/lib/intake/seller-steps";

const listeners = new Set<() => void>();

function subscribe(listener: () => void) {
  listeners.add(listener);
  // The first entry of a page that Next has not yet navigated from carries no router state, so Next ignores
  // Back to it. The address is therefore read straight from the browser.
  window.addEventListener("popstate", listener);
  return () => {
    listeners.delete(listener);
    window.removeEventListener("popstate", listener);
  };
}

function currentToken() {
  return new URLSearchParams(window.location.search).get(SELLER_STEP_KEY) ?? "";
}

function stepHref(step: number) {
  const url = new URL(window.location.href);
  const token = sellerStepToken(step);
  if (token) {
    url.searchParams.set(SELLER_STEP_KEY, token);
  } else {
    url.searchParams.delete(SELLER_STEP_KEY);
  }
  return `${url.pathname}${url.search}${url.hash}`;
}

function writeStep(step: number, mode: "push" | "replace") {
  writeAddress(stepHref(step), null, mode);
  listeners.forEach((listener) => listener());
}

// The stage lives in the address (?step=photos), so a reload or Back returns to it. The address is only ever a
// request: a stage the entered work has not reached is shown, and written back, as the furthest one it has.
// `addressed` is false where the journey is embedded in a page whose address belongs to something else.
export function useSellerStep(reachable: number, addressed = true) {
  const [local, setLocal] = useState(0);
  const token = useSyncExternalStore(subscribe, currentToken, () => "");
  const requested = sellerStepFromToken(token);
  const step = addressed ? Math.min(requested, reachable) : Math.min(local, reachable);

  useEffect(() => {
    if (addressed && requested > reachable) {
      writeStep(reachable, "replace");
    }
  }, [addressed, requested, reachable]);

  function setStep(next: number) {
    if (!addressed) {
      setLocal(next);
      return;
    }
    if (sellerStepToken(next) !== token) {
      writeStep(next, "push");
    }
  }

  return { step, setStep };
}
