import Link from "next/link";
import { buttonClassName } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import type { Branch } from "@/lib/branches";
import styles from "./LandingSellingAndBranches.module.css";
import layout from "./VehicleLayout.module.css";

const SELLING_STEPS = [
  {
    number: "01",
    title: "Get an indicative quote",
    description: "Select your car details and mileage for an instant MYR guide range.",
  },
  {
    number: "02",
    title: "Upload four photos",
    description: "Sign in with a verified account to upload front, rear, and side views.",
  },
  {
    number: "03",
    title: "Book a showroom slot",
    description: "Pick your preferred branch and a 90-minute inspection window.",
  },
  {
    number: "04",
    title: "Confirm final offer",
    description: "Complete a technician check at the hub before accepting your offer.",
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
      <Link href="/sell" prefetch={false} className={buttonClassName("secondary", "md")}>
        {sellLabel}
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
              <p>Book online with an instant guide range and zero obligation before inspection.</p>
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
              <p>Open Monday to Saturday, 09:00–18:00 MYT for test drives and trade-ins.</p>
            </div>
            <Link href="/branches" className={layout.arrowLink}>
              All hub locations <Icon name="next" />
            </Link>
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
                  <BranchCardActions
                    browseHref={`/vehicles?branchId=${branch.id}`}
                    browseLabel="View cars"
                    sellLabel="Book intake"
                  />
                </article>
              ))}
            </div>
          ) : (
            <div className={styles.branchCard}>
              <h3 className={layout.cardHeading}>ApexAuto Malaysian showrooms</h3>
              <p className={styles.branchAddress}>
                Showroom directory details are temporarily unavailable. Browse available vehicles or
                book an intake online.
              </p>
              <BranchCardActions
                browseHref="/vehicles"
                browseLabel="Browse cars"
                sellLabel="Sell your car"
              />
            </div>
          )}
        </div>
      </div>
    </section>
  );
}
