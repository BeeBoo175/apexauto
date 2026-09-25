"use client";

import Image from "next/image";
import { useState } from "react";
import patterns from "@/components/layout/PublicPatterns.module.css";
import { SearchField } from "@/components/ui/SearchField";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import type { FaqEntry } from "@/lib/guide/faq-items";
import { FaqExplorer } from "./FaqExplorer";
import styles from "./HelpCenter.module.css";

export function HelpCenter({ faqEntries }: { faqEntries: readonly FaqEntry[] }) {
  const [query, setQuery] = useState("");

  return (
    <>
      <section className={patterns.hero} aria-labelledby="help-title">
        <Image
          src="/images/inspect-interior.jpg"
          alt=""
          fill
          priority
          sizes="100vw"
          className={patterns.heroPhoto}
        />
        <div className={patterns.heroScrim} aria-hidden="true" />
        <div className={`${layout.content} ${patterns.heroInner} ${patterns.rise}`}>
          <h1 id="help-title" className={layout.heroHeading}>
            FAQ
          </h1>
          <div className={styles.search}>
            <SearchField
              label="Search questions"
              value={query}
              disabled={false}
              onChange={setQuery}
              onClear={query ? () => setQuery("") : undefined}
              placeholder="Try deposit, grade or trade-in"
              density="default"
              size="large"
            />
          </div>
        </div>
      </section>

      <section
        className={`${patterns.band} ${patterns.bandCanvas}`}
        aria-labelledby="faq-list-heading"
      >
        <div className={layout.content}>
          <FaqExplorer faqEntries={faqEntries} query={query} onResetQuery={() => setQuery("")} />
        </div>
      </section>
    </>
  );
}
