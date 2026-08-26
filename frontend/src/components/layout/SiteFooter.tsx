import Link from "next/link";
import { Logo } from "@/components/ui/Logo";
import styles from "./SiteChrome.module.css";

export function SiteFooter() {
  return (
    <footer className={styles.footer}>
      <div className={styles.footerInner}>
        <div className={styles.footerGrid}>
          <div className={styles.footerBrand}>
            <Link href="/" className={styles.footerBrandMark} aria-label="ApexAuto home">
              <Logo />
            </Link>
            <p className={styles.footerTagline}>
              Verified cars, transparent RM pricing, and trusted trade-ins across Malaysia.
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
                <li>
                  <Link href="/buying-guide#warranty">ApexShield warranty</Link>
                </li>
              </ul>
            </div>
            <div className={styles.footerColumn}>
              <h2>Sell &amp; inspect</h2>
              <ul>
                <li>
                  <Link href="/sell" prefetch={false}>
                    Sell your car
                  </Link>
                </li>
                <li>
                  <Link href="/inspection">210-point inspection</Link>
                </li>
                <li>
                  <Link href="/buying-guide#trade-in">How trade-in works</Link>
                </li>
                <li>
                  <Link href="/branches">Showroom branches</Link>
                </li>
              </ul>
            </div>
            <div className={styles.footerColumn}>
              <h2>Company &amp; help</h2>
              <ul>
                <li>
                  <Link href="/buying-guide">Buying guide</Link>
                </li>
                <li>
                  <Link href="/faq">FAQ</Link>
                </li>
                <li>
                  <Link href="/disclaimer">Disclaimers &amp; notices</Link>
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
                  <Link href="/login" prefetch={false}>
                    Customer sign in
                  </Link>
                </li>
                <li>
                  <Link href="/register" prefetch={false}>
                    Create account
                  </Link>
                </li>
              </ul>
            </div>
          </nav>
        </div>
        <div className={styles.footerBottom}>
          <p>&copy; 2026 ApexAuto Sdn Bhd. All rights reserved.</p>
          <p>Malaysia (RM)</p>
        </div>
      </div>
    </footer>
  );
}
