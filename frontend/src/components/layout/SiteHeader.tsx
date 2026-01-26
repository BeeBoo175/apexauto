import Link from "next/link";
import styles from "./SiteChrome.module.css";

export function SiteHeader() {
  return (
    <header className={styles.header}>
      <Link href="/" className={styles.brand} aria-label="ApexAuto home">
        Apex<span>Auto</span>
      </Link>
      <nav className={styles.navigation} aria-label="Main navigation">
        <a href="/account">Account</a>
        <Link href="/vehicles" prefetch={false}>
          Inventory
        </Link>
        <Link href="/sell" prefetch={false}>
          Sell car
        </Link>
        <a href="/dashboard">Staff portal</a>
      </nav>
    </header>
  );
}
