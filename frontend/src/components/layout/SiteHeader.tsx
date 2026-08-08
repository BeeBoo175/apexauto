import Link from "next/link";
import { readCurrentActor } from "@/lib/auth/server";
import styles from "./SiteChrome.module.css";
import { SiteNavigation } from "./SiteNavigation";

export async function SiteHeader() {
  const current = await readCurrentActor().catch(() => ({ status: 502, actor: null }));
  const showAccount = current.status === 401 || Boolean(current.actor);
  const showStaffPortal = Boolean(current.actor && current.actor.role !== "Customer");
  return (
    <header className={styles.header}>
      <Link href="/" className={styles.brand} aria-label="ApexAuto home">
        Apex<span>Auto</span>
      </Link>
      <SiteNavigation showAccount={showAccount} showStaffPortal={showStaffPortal} />
    </header>
  );
}
