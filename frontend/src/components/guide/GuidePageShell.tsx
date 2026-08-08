import type { ReactNode } from "react";
import Link from "next/link";
import { buttonClassName } from "@/components/ui/Button";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import styles from "./GuideLayout.module.css";

export interface HubAction {
  href: string;
  label: string;
  variant: "primary" | "secondary" | "shell";
  prefetch?: boolean;
}

/**
 * Shared dark obsidian header strip across public storefront and guide pages.
 */
export function GuideHubBanner({
  title,
  description,
  actions,
}: {
  title: string;
  description: string;
  actions?: readonly HubAction[];
}) {
  return (
    <header className={styles.hubBanner}>
      <div className={`${layout.content} ${styles.hubBannerInner}`}>
        <div className={styles.hubBannerCopy}>
          <h1 className={layout.pageHeading}>{title}</h1>
          <p>{description}</p>
        </div>
        {actions && actions.length > 0 && (
          <div className={styles.hubBannerActions}>
            {actions.map((action) => (
              <Link
                key={action.href + action.label}
                href={action.href}
                prefetch={action.prefetch ?? false}
                className={buttonClassName(action.variant, "md")}
              >
                {action.label}
              </Link>
            ))}
          </div>
        )}
      </div>
    </header>
  );
}

const bandSurfaceClasses = {
  canvas: styles.bandCanvas,
  paper: styles.bandPaper,
} as const;

export function GuideBand({
  id,
  surface = "canvas",
  headingId,
  ariaLabel,
  heading,
  description,
  children,
}: {
  id?: string;
  surface?: "canvas" | "paper";
  headingId?: string;
  ariaLabel?: string;
  heading?: string;
  description?: string;
  children: ReactNode;
}) {
  const surfaceClass = bandSurfaceClasses[surface];

  return (
    <section
      id={id}
      aria-labelledby={headingId}
      aria-label={headingId ? undefined : ariaLabel}
      className={`${styles.guideBand} ${surfaceClass} ${id ? styles.bandSection : ""}`.trim()}
    >
      <div className={layout.content}>
        {heading && (
          <header className={styles.bandHeader}>
            <h2 id={headingId} className={layout.sectionHeading}>
              {heading}
            </h2>
            {description && <p>{description}</p>}
          </header>
        )}
        {children}
      </div>
    </section>
  );
}
