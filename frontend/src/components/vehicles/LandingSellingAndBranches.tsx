import Link from "next/link";
import { buttonClassName } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import type { Branch } from "@/lib/branches";
import styles from "./LandingSellingAndBranches.module.css";
import layout from "./VehicleLayout.module.css";

const SELLING_STEPS = [
  {
    number: "01",
    title: "Request an indicative quote",
    description:
      "Select your make, model, trim, manufacture year and mileage for an instant MYR guide range.",
  },
  {
    number: "02",
    title: "Upload four exterior photos",
    description:
      "Sign in with a verified customer account and attach clear front, rear and side exterior photographs.",
  },
  {
    number: "03",
    title: "Book a showroom slot",
    description:
      "Pick a convenient ApexAuto branch and time window with live appointment availability.",
  },
  {
    number: "04",
    title: "Complete workshop inspection",
    description:
      "Our showroom team inspects the vehicle in person before confirming your final purchase offer.",
  },
] as const;

function BranchCardActions({
  browseHref,
  browseLabel,
  sellLabel,
}: {
  browseHref: string;
  browseLabel: string;
  sellLabel: string;
}) {
  return (
    <div className={styles.branchActions}>
      <Link href={browseHref} prefetch={false} className={buttonClassName("secondary", "md")}>
        {browseLabel}
      </Link>
      <Link href="/sell" prefetch={false} className={layout.arrowLink}>
        {sellLabel} <Icon name="next" />
      </Link>
    </div>
  );
}

export function LandingSellingAndBranches({ branches }: { branches: Branch[] | null }) {
  return (
    <section
      className={`${layout.band} ${styles.section}`}
      aria-label="Selling process and showrooms"
    >
      <div className={`${layout.content} ${styles.inner}`}>
        <div>
          <header className={styles.blockHeader}>
            <div className={styles.blockHeading}>
              <h2 className={layout.sectionHeading}>Sell your car in four straightforward steps</h2>
              <p>
                Book an intake appointment online with a transparent valuation range and no
                obligation before inspection.
              </p>
            </div>
            <Link href="/sell" prefetch={false} className={buttonClassName("primary", "md")}>
              Start selling your car
            </Link>
          </header>
          <ol className={styles.timeline}>
            {SELLING_STEPS.map((step) => (
              <li key={step.number} className={styles.timelineStep}>
                <span className={styles.stepMarker} aria-hidden="true">
                  {step.number}
                </span>
                <div className={styles.stepContent}>
                  <h3>{step.title}</h3>
                  <p>{step.description}</p>
                </div>
              </li>
            ))}
          </ol>
        </div>

        <div>
          <header className={styles.blockHeader}>
            <div className={styles.blockHeading}>
              <h2 className={layout.sectionHeading}>Visit an ApexAuto showroom</h2>
              <p>
                Browse branch inventory in person, book a test drive or drop off your car for
                trade-in inspection.
              </p>
            </div>
          </header>
          {branches && branches.length > 0 ? (
            <div className={styles.branches}>
              {branches.map((branch) => (
                <article key={branch.id} className={styles.branchCard}>
                  <h3 className={layout.cardHeading}>{branch.name}</h3>
                  <p className={styles.branchAddress}>
                    {branch.address}
                    <br />
                    {branch.city}, {branch.state}
                  </p>
                  <p className={styles.branchPhone}>{branch.phone}</p>
                  <BranchCardActions
                    browseHref={`/vehicles?branchId=${branch.id}`}
                    browseLabel="View showroom cars"
                    sellLabel="Book intake"
                  />
                </article>
              ))}
            </div>
          ) : (
            <div className={styles.branchCard}>
              <h3 className={layout.cardHeading}>ApexAuto Malaysian showrooms</h3>
              <p className={styles.branchAddress}>
                Showroom directory details are temporarily unavailable. Browse our available
                vehicles or start your trade-in booking online.
              </p>
              <BranchCardActions
                browseHref="/vehicles"
                browseLabel="Browse vehicles"
                sellLabel="Sell your car"
              />
            </div>
          )}
        </div>

        <div className={styles.closingBand}>
          <div>
            <h2 className={layout.sectionHeading}>
              Ready to inspect your next car or value your trade-in?
            </h2>
            <p>
              Explore verified listings online, reserve for 48 hours with MYR 500 or start your
              4-step selling appointment.
            </p>
          </div>
          <div className={styles.closingActions}>
            <Link href="/vehicles" prefetch={false} className={buttonClassName("primary", "lg")}>
              Browse all vehicles
            </Link>
            <Link href="/sell" prefetch={false} className={buttonClassName("shell", "lg")}>
              Sell your car
            </Link>
          </div>
        </div>
      </div>
    </section>
  );
}
