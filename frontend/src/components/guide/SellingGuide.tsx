import Image from "next/image";
import Link from "next/link";
import { buttonClassName } from "@/components/ui/Button";
import patterns from "@/components/layout/PublicPatterns.module.css";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import { sellingPageFaq } from "@/lib/guide/faq-items";
import {
  BOOKING_HORIZON_DAYS,
  BOOKING_NOTICE_HOURS,
  BOOKING_NOTICE_LABEL,
  INSPECTION_BAY_MINUTES,
  SHOWROOM_CLOSES_AT,
  SHOWROOM_OPENS_AT,
} from "@/lib/guide/visit-terms";
import { SellingSteps, type SellingStep } from "./SellingSteps";
import { GuideFaq } from "./GuideFaq";
import styles from "./SellingGuide.module.css";

const VISIT_FACTS = [
  { figure: `${INSPECTION_BAY_MINUTES} min`, label: "Showroom bay" },
  { figure: "Mon to Sat", label: `Open ${SHOWROOM_OPENS_AT} to ${SHOWROOM_CLOSES_AT}` },
  {
    figure: `${BOOKING_NOTICE_HOURS} hours`,
    label: `Book ahead, within ${BOOKING_HORIZON_DAYS} days`,
  },
  { figure: "On site", label: "Final offer confirmed" },
] as const;

// Photographs of models in stock imagery, never ApexAuto staff or customers; sources are in public/images/README.md.
const SELL_STEPS: readonly SellingStep[] = [
  {
    id: "details",
    label: "Vehicle details",
    photo: {
      src: "/images/sell-guide-details.jpg",
      alt: "A man leans over an open engine bay in a bright showroom.",
      position: "50% 40%",
    },
    text: "Choose the make, model, trim and year, then enter the mileage and your registration plate. You do not need an account for this step.",
    fieldsTitle: "What you enter",
    fields: [
      "Make",
      "Model",
      "Trim",
      "Manufacture year",
      "Declared mileage (km)",
      "Registration plate",
    ],
    note: "No account needed for this step.",
  },
  {
    id: "valuation",
    label: "Valuation",
    photo: {
      src: "/images/sell-guide-range.jpg",
      alt: "A salesperson helps a customer compare cars in a showroom.",
      position: "50% 35%",
    },
    text: "You get an indicative range straight away, worked out from those details. It is a guide, not an offer. The final offer is confirmed on site after the inspection.",
    fieldsTitle: "What you see",
    fields: ["Indicative valuation range", "Example only, not a quote"],
    note: "A range, not an offer.",
  },
  {
    id: "photos",
    label: "Exterior photos",
    photo: {
      src: "/images/sell-guide-photos.jpg",
      alt: "A man photographs a red car with his smartphone.",
      position: "50% 40%",
    },
    text: "Sign in, then upload four exterior angles of the car: front, driver side, passenger side and rear.",
    fieldsTitle: "The four angles",
    fields: ["Front", "Driver side", "Passenger side", "Rear"],
    note: "Sign in to upload.",
  },
  {
    id: "appointment",
    label: "Appointment",
    photo: {
      src: "/images/sell-guide-visit.jpg",
      alt: "A salesperson and a customer talk about a car in a showroom.",
      position: "50% 35%",
    },
    text: `Pick a showroom and a ${INSPECTION_BAY_MINUTES}-minute bay, Monday to Saturday between ${SHOWROOM_OPENS_AT} and ${SHOWROOM_CLOSES_AT}, at least ${BOOKING_NOTICE_LABEL} ahead and within the next ${BOOKING_HORIZON_DAYS} days.`,
    fieldsTitle: "What you pick",
    fields: ["Showroom", "Day", "Bay"],
    note: `A ${INSPECTION_BAY_MINUTES}-minute bay, booked ${BOOKING_NOTICE_LABEL} ahead.`,
  },
];

export function SellingGuide() {
  return (
    <>
      <section className={patterns.hero} aria-labelledby="selling-guide-title">
        <Image
          src="/images/vehicle-trade-in.webp"
          alt=""
          fill
          priority
          sizes="100vw"
          className={`${patterns.heroPhoto} ${styles.heroPhoto}`}
        />
        <div className={patterns.heroScrim} aria-hidden="true" />
        <div className={`${layout.content} ${patterns.heroInner} ${patterns.rise}`}>
          <h1 id="selling-guide-title" className={layout.heroHeading}>
            How selling works
          </h1>
          <p className={patterns.heroLede}>
            Four short steps online, then one {INSPECTION_BAY_MINUTES}-minute showroom visit. You
            see a price range before you sign in, and the offer is confirmed on site.
          </p>
          <Link href="/sell" prefetch={false} className={buttonClassName("primary", "lg")}>
            Start selling my car
          </Link>
        </div>
      </section>

      <section
        className={`${patterns.band} ${patterns.bandPaper}`}
        aria-label="The four online steps"
      >
        <div className={layout.content}>
          <SellingSteps steps={SELL_STEPS} />
        </div>
      </section>

      <section
        className={`${patterns.band} ${patterns.bandTone}`}
        aria-labelledby="selling-visit-heading"
      >
        <div className={layout.content}>
          <div className={`${styles.visit} ${patterns.riseOnView}`}>
            <div className={styles.visitPhoto}>
              <Image
                src="/images/inspect-brakes.jpg"
                alt="A technician checking brakes during an inspection"
                fill
                quality={85}
                sizes="(max-width: 920px) 100vw, 640px"
                className={styles.visitImage}
              />
            </div>
            <div className={styles.visitCopy}>
              <h2 id="selling-visit-heading" className={layout.sectionHeading}>
                At the showroom
              </h2>
              <p className={patterns.lede}>
                A technician inspects your car in a showroom bay and confirms the final offer on
                site.
              </p>
              <ul className={styles.facts}>
                {VISIT_FACTS.map((fact) => (
                  <li key={fact.label}>
                    <strong>{fact.figure}</strong>
                    <span>{fact.label}</span>
                  </li>
                ))}
              </ul>
              <Link href="/sell" prefetch={false} className={buttonClassName("primary", "md")}>
                Start selling my car
              </Link>
            </div>
          </div>
        </div>
      </section>

      <section
        className={`${patterns.band} ${patterns.bandPaper}`}
        aria-label="Questions about selling"
      >
        <div className={layout.content}>
          <GuideFaq entries={sellingPageFaq} />
        </div>
      </section>
    </>
  );
}
