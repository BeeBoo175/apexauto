import Image from "next/image";
import Link from "next/link";
import detailPhoto from "../../../public/images/apexauto-detail.webp";
import { buttonClassName } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import type { IconName } from "@/lib/icons";
import styles from "./BuyingGuideSections.module.css";

interface PurchaseStep {
  number: string;
  icon: IconName;
  title: string;
  summary: string;
}

const purchaseSteps: readonly PurchaseStep[] = [
  {
    number: "01",
    icon: "inspection",
    title: "Browse 210-pt verified stock",
    summary: "Compare Grade A–C inspection reports, mileage, and fixed pricing online.",
  },
  {
    number: "02",
    icon: "shield-check",
    title: "Reserve with a RM 500 hold",
    summary: "Lock your car for 48 hours. Applied directly toward your cash down payment.",
  },
  {
    number: "03",
    icon: "file-text",
    title: "Confirm loan & trade-in",
    summary: "Apply a showroom trade-in credit and finalize up to 9-year hire-purchase terms.",
  },
  {
    number: "04",
    icon: "car",
    title: "Collect at showroom",
    summary: "Complete JPJ Puspakom paperwork and drive away with ApexShield warranty.",
  },
];

const fixedPriceGuarantees = [
  "Published sticker price with no hidden admin surcharges",
  "Only processing fee (RM 500) carries 8% SST (RM 40)",
  "Optional insurance & extended ApexShield warranty itemized upfront",
] as const;

const handoverDocuments = [
  {
    icon: "user" as IconName,
    title: "Original MyKad / NRIC",
    detail: "Required for JPJ thumbprint verification at transfer.",
  },
  {
    icon: "car" as IconName,
    title: "Valid driving licence",
    detail: "Active Competent Driving Licence (CDL) for test drives & pickup.",
  },
  {
    icon: "file-text" as IconName,
    title: "3 months income documents",
    detail: "Latest payslips plus 3 months bank or EPF statements for hire-purchase.",
  },
  {
    icon: "clock" as IconName,
    title: "Settlement letter (if trading in)",
    detail: "Current hire-purchase balance statement if your trade-in still has a loan.",
  },
] as const;

export function BuyingGuideOverviewSection() {
  return (
    <div className={styles.overviewStack}>
      <ol className={styles.journeyGrid} aria-label="Four steps to buy from ApexAuto">
        {purchaseSteps.map((step) => (
          <li key={step.number} className={styles.journeyCard}>
            <div className={styles.journeyCardTop}>
              <span className={styles.journeyStepNumber} aria-hidden="true">
                {step.number}
              </span>
              <span className={styles.journeyIcon} aria-hidden="true">
                <Icon name={step.icon} />
              </span>
            </div>
            <h3 className={layout.cardHeading}>{step.title}</h3>
            <p>{step.summary}</p>
          </li>
        ))}
      </ol>

      <div className={styles.overviewSplit}>
        <article className={styles.promiseCard}>
          <div className={styles.promiseMedia}>
            <Image
              src={detailPhoto}
              alt="ApexAuto showroom specialist preparing a certified used vehicle for customer handover"
              fill
              sizes="(max-width: 960px) 100vw, 480px"
              className={styles.promisePhoto}
            />
          </div>
          <div className={styles.promiseBody}>
            <div className={styles.promiseHeadingRow}>
              <h3 className={layout.cardHeading}>Fixed-price promise</h3>
              <span className={styles.promiseBadge}>No haggling</span>
            </div>
            <ul className={styles.holdBullets}>
              {fixedPriceGuarantees.map((item) => (
                <li key={item}>
                  <Icon name="check" />
                  <span>{item}</span>
                </li>
              ))}
            </ul>
            <div className={styles.promiseFooter}>
              <Link href="/vehicles" prefetch={false} className={buttonClassName("primary", "md")}>
                Browse inspected cars
              </Link>
              <Link href="#pricing" className={buttonClassName("secondary", "md")}>
                Estimate OTR price
              </Link>
            </div>
          </div>
        </article>

        <article className={styles.documentsCard}>
          <div className={styles.documentsHeader}>
            <h3 className={layout.cardHeading}>Documents to prepare</h3>
            <span className={styles.promiseBadge}>For collection</span>
          </div>
          <p className={styles.documentsLead}>
            Keep these ready before completing your showroom transfer or loan sign-off.
          </p>
          <ul className={styles.documentList}>
            {handoverDocuments.map((doc) => (
              <li key={doc.title} className={styles.documentItem}>
                <span className={styles.documentIcon} aria-hidden="true">
                  <Icon name={doc.icon} />
                </span>
                <div>
                  <strong>{doc.title}</strong>
                  <span>{doc.detail}</span>
                </div>
              </li>
            ))}
          </ul>
          <div className={styles.promiseFooter}>
            <Link href="/branches" className={buttonClassName("secondary", "md")}>
              Find a showroom
            </Link>
          </div>
        </article>
      </div>
    </div>
  );
}
