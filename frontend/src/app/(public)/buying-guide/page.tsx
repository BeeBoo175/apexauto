import type { Metadata } from "next";
import Link from "next/link";
import styles from "./BuyingGuide.module.css";

export const metadata: Metadata = {
  title: "Financing & trade-in guide | ApexAuto",
  description:
    "Understand your vehicle price, indicative financing estimate and trade-in allowance.",
};

export default function BuyingGuidePage() {
  return (
    <article className={styles.page}>
      <Link href="/vehicles" className={styles.back}>
        Browse vehicles
      </Link>
      <header className={styles.heading}>
        <h1>A clearer picture of your next purchase.</h1>
        <p>How pricing, financing estimates and trade-in allowances fit together.</p>
      </header>
      <nav className={styles.contents} aria-label="In this guide">
        <a href="#pricing">Your price</a>
        <a href="#financing">Financing</a>
        <a href="#trade-in">Trade-in</a>
      </nav>
      <section id="pricing" className={styles.section}>
        <h2>Understanding the estimated price</h2>
        <p>
          The vehicle detail page itemizes the vehicle price, processing fee, road tax and SST on
          the processing fee. Its estimated total excludes insurance and optional warranty.
        </p>
        <h3>Applying a promo code</h3>
        <p>
          Codes are case-sensitive, and only one code applies to a quote. A successfully validated
          code appears as a discount in the breakdown and updates the estimated total used by the
          financing calculator. Removing it restores the estimate without that discount.
        </p>
        <p>Validating a promo does not reserve the vehicle or lock its price.</p>
      </section>
      <section id="financing" className={styles.section}>
        <h2>How the financing estimate works</h2>
        <p>
          Adjust your cash contribution and choose a term from one to nine years. The calculator
          starts with a cash contribution of 10% of the displayed total. Your contribution remains
          editable when the estimate changes.
        </p>
        <h3>What goes into the amount financed?</h3>
        <p>
          The calculator subtracts your cash contribution and any applied indicative trade-in
          allowance from the estimated purchase total. The amount financed cannot fall below zero.
        </p>
        <h3>Interest and repayments</h3>
        <p>
          The estimate uses 3% annual flat interest on the amount financed over the selected term.
          Total repayments include that interest. The final installment can differ because of
          rounding; open “Repayment breakdown” on the vehicle page to see the actual calculated
          amounts.
        </p>
        <h3>Including a reservation deposit</h3>
        <p>
          Include any paid MYR 500 reservation deposit in your cash contribution. The calculator
          does not subtract that deposit again.
        </p>
        <p className={styles.note}>
          This calculator provides an indicative estimate, not a financing offer. Final purchase
          terms are subject to confirmation.
        </p>
      </section>
      <section id="trade-in" className={styles.section}>
        <h2>Using your current car as a trade-in</h2>
        <p>
          Enter your current car’s details in the vehicle page’s Trade-in section to request an
          indicative valuation range. After a successful valuation, the lower end of that range
          becomes the allowance used by the financing calculator.
        </p>
        <h3>Your allowance and cash contribution are separate</h3>
        <p>
          The allowance reduces the estimated amount financed in addition to your cash contribution.
          Editing the vehicle details does not change the applied allowance until another valuation
          succeeds. Choose “Get a new valuation” to edit your details and request a replacement.
        </p>
        <h3>What happens after an estimate?</h3>
        <p>
          An indicative valuation does not book an inspection or confirm an acquisition amount. The
          final value is subject to inspection. Use the selling journey if you want to arrange an
          inspection for your car.
        </p>
        <Link href="/sell" className={styles.action}>
          Start the selling journey
        </Link>
      </section>
    </article>
  );
}
