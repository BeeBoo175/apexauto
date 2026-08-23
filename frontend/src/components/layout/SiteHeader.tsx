import Link from "next/link";
import { actorDestination } from "@/lib/auth/actor";
import { readCurrentActor } from "@/lib/auth/server";
import styles from "./SiteChrome.module.css";
import { SiteNavigation } from "./SiteNavigation";

export async function SiteHeader() {
  const current = await readCurrentActor().catch(() => ({ status: 502, actor: null }));
  const showAccount = current.status === 401 || Boolean(current.actor);
  // Staff go straight to their own landing page, so opening the portal skips the redirect through /dashboard.
  const staffPortalHref =
    current.actor && current.actor.role !== "Customer" ? actorDestination(current.actor) : null;
  return (
    <header className={styles.header}>
      <Link href="/" className={styles.brand} aria-label="ApexAuto home">
        Apex<span>Auto</span>
      </Link>
      <SiteNavigation showAccount={showAccount} staffPortalHref={staffPortalHref} />
    </header>
  );
}
