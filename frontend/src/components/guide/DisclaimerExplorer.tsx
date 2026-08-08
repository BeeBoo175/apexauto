import Link from "next/link";
import { Icon } from "@/components/ui/Icon";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import { assetAttributions, disclaimerNotices } from "@/lib/guide/disclaimer-notices";
import styles from "./DisclaimerExplorer.module.css";

export function DisclaimerNoticesGrid() {
  return (
    <div className={styles.policyGrid}>
      {disclaimerNotices.map((notice) => {
        const headingId = `notice-title-${notice.id}`;
        return (
          <section
            key={notice.id}
            id={notice.id}
            aria-labelledby={headingId}
            className={styles.policyItem}
          >
            <div className={styles.policyTitleRow}>
              <span className={styles.policyIcon} aria-hidden="true">
                <Icon name={notice.icon} />
              </span>
              <h3 id={headingId} className={layout.cardHeading}>
                {notice.title}
              </h3>
            </div>
            <p className={styles.policySummary}>{notice.summary}</p>
            <p className={styles.policyDetail}>{notice.detail}</p>
            <div className={styles.policyAction}>
              <Link href={notice.href} prefetch={false} className={layout.arrowLink}>
                <span>{notice.linkLabel}</span>
                <Icon name="next" />
              </Link>
            </div>
          </section>
        );
      })}
    </div>
  );
}

export function AssetAttributionList() {
  return (
    <ul className={styles.creditGrid} aria-label="Photography and UI icon attributions">
      {assetAttributions.map((credit) => (
        <li key={credit.label} className={styles.creditRow}>
          <strong>
            {credit.label} — {credit.author}
          </strong>
          <span>
            <a
              href={credit.sourceUrl}
              target="_blank"
              rel="noreferrer"
              className={styles.creditLink}
            >
              {credit.sourceTitle}
            </a>
            {" · "}
            <a
              href={credit.licenseUrl}
              target="_blank"
              rel="noreferrer"
              className={styles.creditLink}
            >
              {credit.license}
            </a>
          </span>
          <span>{credit.changes}</span>
        </li>
      ))}
    </ul>
  );
}
