"use client";

import { useEffect } from "react";

export interface WorkspaceTab<T extends string> {
  value: T;
  label: string;
  available: boolean;
}

export function useWorkspaceTabs<T extends string>(
  requested: T,
  options: readonly WorkspaceTab<T>[],
  fallback: T,
  onChange: (section: T) => void,
) {
  const tabs = options.filter((tab) => tab.available);
  const section = tabs.some((tab) => tab.value === requested) ? requested : fallback;

  useEffect(() => {
    let current = true;
    if (section !== requested) {
      queueMicrotask(() => {
        if (current) {
          onChange(section);
        }
      });
    }
    return () => {
      current = false;
    };
  }, [requested, section, onChange]);

  return { tabs, section };
}
