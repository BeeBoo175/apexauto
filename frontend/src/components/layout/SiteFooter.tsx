import Link from "next/link";
import styles from "./SiteChrome.module.css";

export function SiteFooter() {
  return (
    <footer className={styles.footer}>
      <div className={styles.footerInner}>
        <div className={styles.footerGrid}>
          <div className={styles.footerBrand}>
            <Link href="/" className={styles.footerBrandMark} aria-label="ApexAuto home">
              Apex<span>Auto</span>
            </Link>
            <p className={styles.footerTagline}>
              Inspected used cars, itemized Malaysian pricing and showroom trade-ins across
              Malaysia.
            </p>
          </div>
          <nav className={styles.footerColumns} aria-label="Footer navigation">
            <div className={styles.footerColumn}>
              <h2>Buy a car</h2>
              <ul>
                <li>
                  <Link href="/vehicles" prefetch={false}>
                    Browse all cars
                  </Link>
                </li>
                <li>
                  <Link href="/buying-guide#pricing">Pricing and fees</Link>
                </li>
                <li>
                  <Link href="/buying-guide#financing">Financing guide</Link>
                </li>
              </ul>
            </div>
            <div className={styles.footerColumn}>
              <h2>Sell &amp; trade in</h2>
              <ul>
                <li>
                  <Link href="/sell" prefetch={false}>
                    Sell your car
                  </Link>
                </li>
                <li>
                  <Link href="/buying-guide#trade-in">How trade-in works</Link>
                </li>
                <li>
                  <Link href="/buying-guide">Buying guide</Link>
                </li>
              </ul>
            </div>
            <div className={styles.footerColumn}>
              <h2>Account</h2>
              <ul>
                <li>
                  <Link href="/account" prefetch={false}>
                    Your reservations
                  </Link>
                </li>
                <li>
                  <Link href="/login">Customer sign in</Link>
                </li>
                <li>
                  <Link href="/register">Create account</Link>
                </li>
              </ul>
            </div>
          </nav>
        </div>
        <div className={styles.footerBottom}>
          <p>&copy; 2026 ApexAuto Sdn Bhd. All rights reserved.</p>
          <p>Malaysia (MYR)</p>
        </div>
      </div>
    </footer>
  );
}
