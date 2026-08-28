import Image from "next/image";
import Link from "next/link";
import tradeInPhoto from "../../../public/images/vehicle-trade-in.webp";
import { buttonClassName } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import type { IconName } from "@/lib/icons";
import sectionStyles from "./BuyingGuideSections.module.css";
import styles from "./BuyingGuideTradeIn.module.css";

const intakeSteps: readonly { icon: IconName; label: string; note: string }[] = [
  {
    icon: "car",
    label: "4 exterior photos",
    note: "Driver, passenger, front, and rear.",
  },
  {
    icon: "calendar",
    label: "90-minute bay slot",
    note: "Book 2h ahead, Mon–Sat 09:00–18:00.",
  },
  {
    icon: "shield-check",
    label: "Conservative credit",
    note: "Applies your range lower bound until check-in.",
  },
  {
    icon: "file-text",
    label: "Documents on arrival",
    note: "Registration card, keys, and NRIC / ID.",
  },
];

export function BuyingGuideTradeInSection() {
  return (
    <div className={sectionStyles.split}>
      <div className={styles.prepColumn}>
        <ul className={styles.prepList} aria-label="What to prepare for trade-in">
          {intakeSteps.map((item) => (
            <li key={item.label} className={styles.prepItem}>
              <span className={styles.prepIcon} aria-hidden="true">
                <Icon name={item.icon} />
              </span>
              <span className={styles.prepCopy}>
                <strong>{item.label}</strong>
                <span>{item.note}</span>
              </span>
            </li>
          ))}
        </ul>

        <div className={styles.prepActions}>
          <Link href="/sell" prefetch={false} className={buttonClassName("primary", "md")}>
            Get an instant quote
          </Link>
          <Link href="/branches" className={layout.arrowLink}>
            <span>Check branch hours</span>
            <Icon name="next" />
          </Link>
        </div>
      </div>

      <div className={styles.tradeInPhoto}>
        <Image
          src={tradeInPhoto}
          alt="Driver holding a car key beside a vehicle prepared for showroom appraisal"
          fill
          loading="eager"
          sizes="(max-width: 920px) 100vw, 560px"
        />
      </div>
    </div>
  );
}
