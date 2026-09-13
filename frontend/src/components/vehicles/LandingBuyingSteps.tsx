import Image from "next/image";
import Link from "next/link";
import patterns from "@/components/layout/PublicPatterns.module.css";
import { Icon } from "@/components/ui/Icon";
import { formatWholeCurrency } from "@/lib/currency-format";
import { RESERVATION_DEPOSIT, RESERVATION_HOLD_HOURS } from "@/lib/vehicles/deposit-terms";
import styles from "./LandingBuyingSteps.module.css";
import layout from "./VehicleLayout.module.css";

const STEPS = [
  {
    title: "Choose and reserve",
    description: `Test drive it first if you like. ${formatWholeCurrency(RESERVATION_DEPOSIT)} then holds it for ${RESERVATION_HOLD_HOURS} hours, fully refundable.`,
    image: "/images/step-choose.jpg",
    alt: "A customer and a showroom adviser looking over a car together",
    focus: "45% 40%",
  },
  {
    title: "Agree final terms",
    description: "Our sales team calls or emails to confirm price, trade-in and payment.",
    image: "/images/step-reserve.jpg",
    alt: "A customer checking his phone beside a parked car",
    focus: "50% 45%",
  },
  {
    title: "Visit and collect",
    description: "Book your showroom visit, settle the balance and drive away.",
    image: "/images/step-keys.jpg",
    alt: "A smiling customer holding up her new car key from the driver's seat",
    focus: "48% 50%",
  },
] as const;

export function LandingBuyingSteps() {
  return (
    <section
      className={`${patterns.band} ${patterns.bandDark}`}
      aria-labelledby="buying-steps-heading"
    >
      <div className={layout.content}>
        <div className={styles.head}>
          <div>
            <h2 id="buying-steps-heading" className={patterns.sectionTitle}>
              How buying works
            </h2>
            <p className={patterns.sectionSub}>Choose, reserve, then collect at the showroom.</p>
          </div>
          <Link href="/buying-guide" className={styles.more}>
            Read the full guide <Icon name="next" />
          </Link>
        </div>

        <ol className={styles.film}>
          {STEPS.map((step, index) => (
            <li
              key={step.title}
              className={styles.step}
              style={{ "--step-index": index } as React.CSSProperties}
            >
              <div className={`${patterns.photo} ${styles.media}`}>
                <Image
                  src={step.image}
                  alt={step.alt}
                  fill
                  sizes="(max-width: 920px) 100vw, 640px"
                  style={{ objectPosition: step.focus }}
                />
                <span className={styles.number} aria-hidden="true">
                  {String(index + 1).padStart(2, "0")}
                </span>
              </div>
              <div className={styles.caption}>
                <h3>{step.title}</h3>
                <p>{step.description}</p>
              </div>
            </li>
          ))}
        </ol>
      </div>
    </section>
  );
}
