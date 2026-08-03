"use client";

import { useState } from "react";
import { usePathname, useSearchParams } from "next/navigation";
import { writeAddress } from "@/lib/browser-url";
import { staffRecordLocation } from "@/lib/staff/record-location";

export function useRecordSection<T extends string>(
  workspace: string,
  id: string,
  sections: readonly [T, ...T[]],
) {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const location = staffRecordLocation(pathname, workspace);
  const requested = sections.find((section) => section === location?.section) ?? sections[0];
  const [confirmed, setConfirmed] = useState({ id, section: requested });
  const section = location?.id === id ? requested : confirmed.section;
  if (confirmed.id !== id || confirmed.section !== section) {
    setConfirmed({ id, section });
  }

  function choose(next: T) {
    if (location?.id !== id) {
      return;
    }
    const suffix = next === sections[0] ? "" : `/${next}`;
    const query = searchParams.toString();
    const href = `${workspace}/${id}${suffix}${query ? `?${query}` : ""}`;
    if (`${window.location.pathname}${window.location.search}` !== href) {
      writeAddress(href, null, "push");
    }
  }

  return [section, choose] as const;
}
