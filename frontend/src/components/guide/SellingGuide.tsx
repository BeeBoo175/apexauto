import Image from "next/image";
import Link from "next/link";
import { buttonClassName } from "@/components/ui/Button";
import patterns from "@/components/layout/PublicPatterns.module.css";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import { sellingPageFaq } from "@/lib/guide/faq-items";
import { SellingSteps, type SellingStep } from "./SellingSteps";
import { WarrantyFaq } from "./WarrantyFaq";
import styles from "./SellingGuide.module.css";

const VISIT_FACTS = [
  { figure: "90 min", label: "Showroom bay" },
  { figure: "Mon to Sat", label: "Open 09:00 to 18:00" },
  { figure: "2 hours", label: "Book ahead, within 30 days" },
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
    text: "Pick a showroom and a 90-minute bay, Monday to Saturday between 09:00 and 18:00, at least two hours ahead and within the next 30 days.",
    fieldsTitle: "What you pick",
    fields: ["Showroom", "Day", "Bay"],
    note: "A 90-minute bay, booked two hours ahead.",
  },
];

export function SellingGuide() {
  return (
    <>
      <section className={styles.hero} aria-labelledby="selling-guide-title">
        <Image
          src="/images/vehicle-trade-in.webp"
          alt=""
          fill
          priority
          sizes="100vw"
          className={styles.heroPhoto}
        />
        <div className={styles.heroScrim} aria-hidden="true" />
        <div className={`${layout.content} ${styles.heroInner} ${patterns.rise}`}>
          <h1 id="selling-guide-title" className={layout.heroHeading}>
            How selling works
          </h1>
          <p className={styles.lede}>
            Four short steps online, then one 90-minute showroom visit. You see a price range before
            you sign in, and the offer is confirmed on site.
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
        className={`${patterns.band} ${styles.visitBand}`}
        aria-labelledby="selling-visit-heading"
      >
        <div className={layout.content}>
          <div className={styles.visit}>
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
          <WarrantyFaq entries={sellingPageFaq} />
        </div>
      </section>
    </>
  );
}
