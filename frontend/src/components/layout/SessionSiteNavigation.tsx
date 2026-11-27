"use client";

import { useSiteAccount } from "@/hooks/use-site-account";
import { SiteNavigation } from "./SiteNavigation";

export function SessionSiteNavigation() {
  const links = useSiteAccount();

  return (
    <SiteNavigation
      accountPending={links === null}
      showAccount={links?.showAccount ?? false}
      staffPortalHref={links?.staffPortalHref ?? null}
    />
  );
}
