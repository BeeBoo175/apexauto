"use client";

import type { KeyboardEvent } from "react";

const KEY_OFFSETS: Record<string, number> = { ArrowRight: 1, ArrowLeft: -1 };

interface RovingTabsOptions {
  count: number;
  activeIndex: number;
  // Past the last tab the focus returns to the first one, and the other way round; otherwise it stops at the ends.
  wrap: boolean;
  tabId: (index: number) => string;
  onSelect: (index: number) => void;
}

// The arrow-key handler for a tab list: it selects the neighbouring tab and moves focus to it.
export function useRovingTabs({ count, activeIndex, wrap, tabId, onSelect }: RovingTabsOptions) {
  return function onKeyDown(event: KeyboardEvent<HTMLElement>) {
    const offset = KEY_OFFSETS[event.key];
    if (offset === undefined) {
      return;
    }
    event.preventDefault();
    const next = activeIndex + offset;
    const target = wrap ? (next + count) % count : Math.min(Math.max(next, 0), count - 1);
    onSelect(target);
    document.getElementById(tabId(target))?.focus();
  };
}
