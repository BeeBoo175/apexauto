import styles from "./Home.module.css";
import Link from "next/link";
import buttonStyles from "@/components/ui/Button.module.css";
import { Card } from "@/components/ui/Card";
import { Badge } from "@/components/ui/Badge";

export default function Home() {
  return (
    <div className={styles.page}>
      <section className={styles.hero}>
        <div className={styles.eyebrow}>
          <Badge variant="trust">Malaysia Retail DMS</Badge>
          <span className={styles.subtitle}>Verified Single Record Lifecycle</span>
        </div>
        <h1 className={styles.title}>
          The Editorial <span className={styles.accent}>Service Bay</span>
        </h1>
        <p className={styles.introduction}>
          High-precision dealership operations connecting public customer intake directly to
          standardized 210-point inspections, storefront 48-hour reservations, and Deal Desk
          contract closing.
        </p>
        <div className={styles.actions}>
          <a
            href="/vehicles"
            className={`${buttonStyles.button} ${buttonStyles.primary} ${buttonStyles.lg}`}
          >
            Browse vehicles
          </a>
          <Link
            href="/sell"
            prefetch={false}
            className={`${buttonStyles.button} ${buttonStyles.secondary} ${buttonStyles.lg}`}
          >
            Instant Trade-In Valuation
          </Link>
        </div>
      </section>

      <section className={styles.stages}>
        <Card>
          <span className={styles.stageNumber}>Stage 01</span>
          <h3 className={styles.stageTitle}>Intake &amp; Appraisal</h3>
          <p className={styles.stageDescription}>
            Carsome-style 4-step wizard with pure C# algorithmic depreciation curves and S3 photo
            capture.
          </p>
        </Card>

        <Card>
          <span className={styles.stageNumber}>Stage 02</span>
          <h3 className={styles.stageTitle}>Inspection &amp; Recond</h3>
          <p className={styles.stageDescription}>
            Static 210 checklist catalog with defect exception logging and Puspakom/B5 grading.
          </p>
        </Card>

        <Card>
          <span className={styles.stageNumber}>Stage 03</span>
          <h3 className={styles.stageTitle}>Storefront &amp; Hold</h3>
          <p className={styles.stageDescription}>
            RSC vehicle catalogue with live trade-in loan offset and 48-hour row-locked reservation.
          </p>
        </Card>

        <Card>
          <span className={styles.stageNumber}>Stage 04</span>
          <h3 className={styles.stageTitle}>Deal Desk &amp; Closing</h3>
          <p className={styles.stageDescription}>
            Itemized VPO contract with discount gate, bank LOU settlement, and printable receipts.
          </p>
        </Card>
      </section>
    </div>
  );
}
