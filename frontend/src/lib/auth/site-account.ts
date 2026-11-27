import { isRecord } from "./contracts";
import { actorDestination, type CurrentActor } from "./actor";

export interface SiteAccountLinks {
  showAccount: boolean;
  staffPortalHref: string | null;
}

export const HIDDEN_SITE_ACCOUNT_LINKS: SiteAccountLinks = {
  showAccount: false,
  staffPortalHref: null,
};

export function siteAccountLinks(current: {
  status: number;
  actor: CurrentActor | null;
}): SiteAccountLinks {
  const showAccount = current.status === 401 || Boolean(current.actor);
  // Staff go straight to their own landing page, so opening the portal skips the redirect through /dashboard.
  const staffPortalHref =
    current.actor && current.actor.role !== "Customer" ? actorDestination(current.actor) : null;
  return { showAccount, staffPortalHref };
}

export function parseSiteAccountLinks(value: unknown): SiteAccountLinks | null {
  if (!isRecord(value) || typeof value.showAccount !== "boolean") {
    return null;
  }
  const href = value.staffPortalHref;
  if (href !== null && !(typeof href === "string" && href.startsWith("/dashboard/"))) {
    return null;
  }
  return { showAccount: value.showAccount, staffPortalHref: href };
}
