"use client";

import { useState } from "react";

// The open tab of a drawer rides in the address (`?section=history`), so a reload or a shared link returns to it.
// The first section is the default and is never written, which keeps ordinary addresses clean. Read the address
// only in the browser: drawers mount after a browser read, never during server rendering.
export function useAddressSection<T extends string>(
  sections: readonly [T, ...T[]],
  parameter = "section",
) {
  const [section, setSection] = useState<T>(() => {
    const requested = new URL(window.location.href).searchParams.get(parameter);
    return sections.find((item) => item === requested) ?? sections[0];
  });

  function choose(next: T) {
    setSection(next);
    const url = new URL(window.location.href);
    if (next === sections[0]) {
      url.searchParams.delete(parameter);
    } else {
      url.searchParams.set(parameter, next);
    }
    window.history.replaceState(null, "", url);
  }

  return [section, choose] as const;
}
