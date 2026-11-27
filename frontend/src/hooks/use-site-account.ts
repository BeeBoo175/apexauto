"use client";

import { useEffect, useState } from "react";
import { apiFetch } from "@/lib/api-browser";
import {
  HIDDEN_SITE_ACCOUNT_LINKS,
  parseSiteAccountLinks,
  type SiteAccountLinks,
} from "@/lib/auth/site-account";

// Null while the answer is pending. A failed read hides the links, as the server header does.
export function useSiteAccount() {
  const [links, setLinks] = useState<SiteAccountLinks | null>(null);

  useEffect(() => {
    const attempt = new AbortController();
    apiFetch("/api/auth/session", parseSiteAccountLinks, { signal: attempt.signal })
      .then((result) => {
        if (!attempt.signal.aborted) {
          setLinks(result.data ?? HIDDEN_SITE_ACCOUNT_LINKS);
        }
      })
      .catch(() => {
        if (!attempt.signal.aborted) {
          setLinks(HIDDEN_SITE_ACCOUNT_LINKS);
        }
      });
    return () => attempt.abort();
  }, []);

  return links;
}
