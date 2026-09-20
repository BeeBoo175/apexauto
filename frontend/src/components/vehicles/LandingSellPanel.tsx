"use client";

import { useEffect, useRef, useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { useRouter } from "next/navigation";
import patterns from "@/components/layout/PublicPatterns.module.css";
import { Select } from "@/components/ui/Input";
import type { Taxonomy } from "@/lib/intake/contracts";
import { malaysianYear } from "@/lib/intake/manufacture-years";
import styles from "./LandingSellPanel.module.css";
import layout from "./VehicleLayout.module.css";

const STEPS = [
  { num: "01", title: "Instant online range" },
  { num: "02", title: "90-minute showroom inspection" },
  { num: "03", title: "Final offer on site" },
] as const;

// The years the selected make has been sold, from its earliest trim to now, matching the sell form.
function sellableYears(taxonomy: Taxonomy | null | undefined, makeName: string) {
  const trims = (taxonomy?.makes ?? [])
    .filter((item) => !makeName || item.name === makeName)
    .flatMap((item) => item.models.flatMap((model) => model.trims));
  if (trims.length === 0) {
    return [];
  }
  const newest = malaysianYear(new Date());
  const oldest = Math.min(...trims.map((trim) => trim.firstYear));
  return Array.from({ length: Math.max(0, newest - oldest + 1) }, (_, index) => newest - index);
}

interface LandingSellPanelProps {
  taxonomy?: Taxonomy | null;
}

export function LandingSellPanel({ taxonomy }: LandingSellPanelProps) {
  const router = useRouter();
  const [make, setMake] = useState("");
  const [year, setYear] = useState("");
  // The car waits off to the right until the panel is in view, then drives in once.
  const stage = useRef<HTMLDivElement>(null);
  const [driven, setDriven] = useState(false);

  useEffect(() => {
    const element = stage.current;
    if (!element) {
      return;
    }
    const observer = new IntersectionObserver(
      (entries) => {
        // Entries can arrive in a batch, so any one of them being in view is enough.
        if (entries.some((entry) => entry.isIntersecting)) {
          observer.disconnect();
          setDriven(true);
        }
      },
      { threshold: 0.35 },
    );
    observer.observe(element);
    return () => observer.disconnect();
  }, []);

  const makes = taxonomy?.makes.map((item) => item.name) ?? [];
  const years = sellableYears(taxonomy, make);

  function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const params = new URLSearchParams();
    if (make) {
      params.set("make", make);
    }
    if (year) {
      params.set("year", year);
    }
    const query = params.toString();
    router.push(query ? `/sell?${query}` : "/sell");
  }

  return (
    <section className={`${patterns.band} ${styles.root}`} aria-labelledby="sell-panel-heading">
      <div className={layout.content}>
        <div className={styles.banner}>
          {/* Main Content Area: Copy + Capsule Form + Car Studio Render */}
          <div className={styles.mainArea}>
            <div className={styles.copyCol}>
              <h2 id="sell-panel-heading" className={styles.title}>
                Sell your car
              </h2>
              <p className={styles.subcopy}>
                See a price range before you sign in. The figure is confirmed at a showroom
                inspection.
              </p>

              {/* Quick Valuation Input Capsule */}
              <form className={styles.capsule} onSubmit={handleSubmit} method="get" action="/sell">
                <div className={styles.selectCell}>
                  <Select
                    name="make"
                    label="Select Make"
                    labelHidden
                    value={make}
                    onChange={(e) => setMake(e.target.value)}
                    className={styles.select}
                  >
                    <option value="">Select Make</option>
                    {makes.map((m) => (
                      <option key={m} value={m}>
                        {m}
                      </option>
                    ))}
                  </Select>
                </div>

                <span className={styles.capsuleDivider} aria-hidden="true" />

                <div className={styles.selectCell}>
                  <Select
                    name="year"
                    label="Select Year"
                    labelHidden
                    value={year}
                    onChange={(e) => setYear(e.target.value)}
                    className={styles.select}
                  >
                    <option value="">Year</option>
                    {years.map((y) => (
                      <option key={y} value={y}>
                        {y}
                      </option>
                    ))}
                  </Select>
                </div>

                <button type="submit" className={styles.capsuleButton}>
                  Get my price range
                </button>
              </form>

              {/* Footnote */}
              <p className={styles.footnote}>
                Free, not an offer, and no obligation to sell.{" "}
                <Link href="/selling-guide" className={styles.footnoteLink}>
                  See how selling works
                </Link>
              </p>
            </div>

            {/* Right Side: Clean Sedan Cutout with Dynamic Overhang */}
            <div ref={stage} className={styles.visualCol}>
              <Image
                src="/images/sell-car-stage.png"
                alt="Silver BMW sports sedan"
                width={900}
                height={360}
                unoptimized
                className={styles.carImage}
                data-drive={driven ? "arrived" : "waiting"}
              />
            </div>
          </div>

          {/* Bottom Bar: 3-Step Inset Strip */}
          <div className={styles.bottomBar}>
            {STEPS.map((step) => (
              <div key={step.num} className={styles.bottomStep}>
                <span className={styles.bottomNum} aria-hidden="true">
                  {step.num}
                </span>
                <span className={styles.bottomLabel}>{step.title}</span>
              </div>
            ))}
          </div>
        </div>
      </div>
    </section>
  );
}
