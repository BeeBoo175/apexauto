import Link from "next/link";
import { Logo } from "@/components/ui/Logo";
import { SITE_CONTACT } from "@/lib/site-contact";
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
              ApexAuto is a used car dealer with showrooms in Selangor and Pulau Pinang. Buy an
              inspected car, book a test drive, or sell or trade in yours.
            </p>
            <address className={styles.footerContact}>
              <a href={`mailto:${SITE_CONTACT.email}`}>{SITE_CONTACT.email}</a>
              <a href={`tel:${SITE_CONTACT.phoneHref}`}>{SITE_CONTACT.phone}</a>
              <span>{SITE_CONTACT.hours}</span>
            </address>
          </div>
          <nav className={styles.footerColumns} aria-label="Footer navigation">
            <div className={styles.footerColumn}>
              <h2>Buy</h2>
              <ul>
                <li>
                  <Link href="/vehicles" prefetch={false}>
                    Browse all cars
                  </Link>
                </li>
                <li>
                  <Link href="/buying-guide">How buying works</Link>
                </li>
                <li>
                  <Link href="/inspection">210-point inspection</Link>
                </li>
                <li>
                  <Link href="/warranty">ApexShield warranty</Link>
                </li>
              </ul>
            </div>
            <div className={styles.footerColumn}>
              <h2>Sell</h2>
              <ul>
                <li>
                  <Link href="/sell" prefetch={false}>
                    Sell my car
                  </Link>
                </li>
                <li>
                  <Link href="/selling-guide">How selling works</Link>
                </li>
                <li>
                  <Link href="/branches">Book an appraisal</Link>
                </li>
              </ul>
            </div>
            <div className={styles.footerColumn}>
              <h2>Support</h2>
              <ul>
                <li>
                  <Link href="/faq">FAQ</Link>
                </li>
                <li>
                  <a href={`mailto:${SITE_CONTACT.email}`}>Contact us</a>
                </li>
                <li>
                  <Link href="/branches">Locate us</Link>
                </li>
                <li>
                  <Link href="/login" prefetch={false}>
                    Customer sign in
                  </Link>
                </li>
                <li>
                  <Link href="/account" prefetch={false}>
                    Your reservations
                  </Link>
                </li>
              </ul>
            </div>
            <div className={styles.footerColumn}>
              <h2>About ApexAuto</h2>
              <ul>
                <li>
                  <Link href="/about">Our story</Link>
                </li>
                <li>
                  <Link href="/register" prefetch={false}>
                    Create an account
                  </Link>
                </li>
                <li>
                  <Link href="/disclaimer">Disclaimers &amp; notices</Link>
                </li>
              </ul>
            </div>
          </nav>
        </div>
        <div className={styles.footerBottom}>
          <p>&copy; 2026 ApexAuto Sdn Bhd. A portfolio demonstration, not a real dealership.</p>
          <nav className={styles.footerLegal} aria-label="Legal">
            <Link href="/privacy">Privacy policy</Link>
            <Link href="/terms">Terms of use</Link>
          </nav>
        </div>
      </div>
    </footer>
  );
}
