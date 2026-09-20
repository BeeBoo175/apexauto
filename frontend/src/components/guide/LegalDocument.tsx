import patterns from "@/components/layout/PublicPatterns.module.css";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import type { LegalSection } from "@/lib/legal/legal-types";
import { LEGAL_UPDATED } from "@/lib/site-contact";
import styles from "./LegalDocument.module.css";

// A plain page of numbered text for a legal document: the title and date, an opening paragraph, then each section.
export function LegalDocument({
  title,
  intro,
  sections,
}: {
  title: string;
  intro: string;
  sections: readonly LegalSection[];
}) {
  return (
    <section className={`${patterns.band} ${patterns.bandPaper}`} aria-labelledby="legal-title">
      <div className={`${layout.content} ${styles.document}`}>
        <header className={styles.header}>
          <h1 id="legal-title">{title}</h1>
          <p className={styles.updated}>Last updated {LEGAL_UPDATED}</p>
        </header>
        <p className={styles.intro}>{intro}</p>
        <div className={styles.sections}>
          {sections.map((section, index) => (
            <section key={section.heading} className={styles.section}>
              <h2>
                <span aria-hidden="true">{index + 1}.</span> {section.heading}
              </h2>
              {section.paragraphs.map((paragraph) => (
                <p key={paragraph}>{paragraph}</p>
              ))}
              {section.items && (
                <ul>
                  {section.items.map((item) => (
                    <li key={item}>{item}</li>
                  ))}
                </ul>
              )}
              {section.after?.map((paragraph) => (
                <p key={paragraph}>{paragraph}</p>
              ))}
            </section>
          ))}
        </div>
      </div>
    </section>
  );
}
