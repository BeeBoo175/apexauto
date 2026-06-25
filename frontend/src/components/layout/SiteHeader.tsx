import Link from "next/link";
import { readCurrentActor } from "@/lib/auth/server";
import styles from "./SiteChrome.module.css";

export async function SiteHeader() {
  const current = await readCurrentActor().catch(() => ({ status: 502, actor: null }));
  const showAccount = current.status === 401 || Boolean(current.actor);
  const showStaffPortal = Boolean(current.actor && current.actor.role !== "Customer");
  return (
    <header className={styles.header}>
      <Link href="/" className={styles.brand} aria-label="ApexAuto home">
        Apex<span>Auto</span>
      </Link>
      <nav className={styles.navigation} aria-label="Main navigation">
        <Link href="/vehicles" prefetch={false}>
          Buy car
        </Link>
        {showAccount && <a href="/account">Account</a>}
        <Link href="/sell" prefetch={false}>
          Sell car
        </Link>
        {showStaffPortal && <a href="/dashboard">Staff portal</a>}
      </nav>
    </header>
  );
}
