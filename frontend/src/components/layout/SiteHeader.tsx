import Link from "next/link";
import { Logo } from "@/components/ui/Logo";
import { SessionSiteNavigation } from "./SessionSiteNavigation";
import styles from "./SiteChrome.module.css";

// Reads no cookies here, so public content pages can be generated ahead of time.
export function SiteHeader() {
  return (
    <header className={styles.header}>
      <Link href="/" className={styles.brand} aria-label="ApexAuto home">
        <Logo />
      </Link>
      <SessionSiteNavigation />
    </header>
  );
}
