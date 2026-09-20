"use client";

import { useState, useSyncExternalStore } from "react";
import Link from "next/link";
import patterns from "@/components/layout/PublicPatterns.module.css";
import { Icon } from "@/components/ui/Icon";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import { assetAttributions, disclaimerNotices } from "@/lib/guide/disclaimer-notices";
import type { IconName } from "@/lib/icons";
import faq from "./FaqExplorer.module.css";
import styles from "./HelpCenter.module.css";

type NoticeTopic = "All" | "Notices" | "Credits";

const topics: readonly { name: NoticeTopic; icon: IconName }[] = [
  { name: "All", icon: "help" },
  { name: "Notices", icon: "shield-check" },
  { name: "Credits", icon: "file-text" },
];

const CREDITS_ID = "credits";

function subscribeToHash(onChange: () => void) {
  window.addEventListener("hashchange", onChange);
  return () => window.removeEventListener("hashchange", onChange);
}

// The disclaimers laid out like the FAQ: a topic rail beside hairline rows that open and close.
export function HelpNotices() {
  const [topic, setTopic] = useState<NoticeTopic>("All");
  const [openIds, setOpenIds] = useState<string[]>(disclaimerNotices.map((notice) => notice.id));
  // A link to the credits opens their row, since it would otherwise land on a closed one. Once the visitor opens or
  // closes the row themselves, that choice wins over the link.
  const linkedToCredits = useSyncExternalStore(
    subscribeToHash,
    () => window.location.hash === `#${CREDITS_ID}`,
    () => false,
  );
  const [creditsChoice, setCreditsChoice] = useState<boolean | null>(null);
  const creditsOpen = creditsChoice ?? linkedToCredits;

  const showNotices = topic !== "Credits";
  const showCredits = topic !== "Notices";

  function countFor(name: NoticeTopic) {
    if (name === "Notices") {
      return disclaimerNotices.length;
    }
    return name === "Credits" ? 1 : disclaimerNotices.length + 1;
  }

  function toggle(id: string) {
    setOpenIds((current) =>
      current.includes(id) ? current.filter((item) => item !== id) : [...current, id],
    );
  }

  return (
    <section
      id="notices"
      className={`${patterns.band} ${patterns.bandCanvas}`}
      aria-labelledby="notices-heading"
    >
      <div className={layout.content}>
        <div className={faq.faqSplit}>
          <aside className={faq.faqRail} aria-label="Notice topics">
            <div>
              <p className={faq.railHeading}>Topics</p>
              <div className={faq.topicNav} role="group" aria-label="Filter notice topics">
                {topics.map((item) => (
                  <button
                    key={item.name}
                    type="button"
                    className={faq.topicButton}
                    aria-pressed={topic === item.name}
                    onClick={() => setTopic(item.name)}
                  >
                    <span className={faq.topicLabel}>
                      <span className={faq.topicIcon} aria-hidden="true">
                        <Icon name={item.icon} />
                      </span>
                      <span>{item.name}</span>
                    </span>
                    <span className={faq.topicCount}>{countFor(item.name)}</span>
                  </button>
                ))}
              </div>
            </div>
          </aside>

          <div className={faq.faqMain}>
            <div className={faq.faqTopBar}>
              <div className={faq.resultsMeta}>
                <h2 id="notices-heading" className={faq.resultsHeading}>
                  {topic === "All" ? "All notices" : topic}
                </h2>
              </div>
            </div>

            <div className={faq.hairlineList}>
              {showNotices &&
                disclaimerNotices.map((notice) => {
                  const expanded = openIds.includes(notice.id);
                  const panelId = `notice-panel-${notice.id}`;
                  return (
                    <article key={notice.id} id={notice.id} className={faq.accordionRow}>
                      <button
                        type="button"
                        className={faq.accordionTrigger}
                        aria-expanded={expanded}
                        aria-controls={panelId}
                        onClick={() => toggle(notice.id)}
                      >
                        <span className={faq.triggerQuestion}>{notice.title}</span>
                        <span className={faq.triggerIcon} aria-hidden="true">
                          <Icon name="down" />
                        </span>
                      </button>
                      {expanded && (
                        <div id={panelId} className={faq.accordionBody}>
                          <p>
                            {notice.summary} {notice.detail}
                          </p>
                          <Link href={notice.href} prefetch={false} className={layout.arrowLink}>
                            <span>{notice.linkLabel}</span>
                            <Icon name="next" />
                          </Link>
                        </div>
                      )}
                    </article>
                  );
                })}

              {showCredits && (
                <article id={CREDITS_ID} className={faq.accordionRow}>
                  <button
                    type="button"
                    className={faq.accordionTrigger}
                    aria-expanded={creditsOpen}
                    aria-controls="notice-panel-credits"
                    onClick={() => setCreditsChoice(!creditsOpen)}
                  >
                    <span className={faq.triggerQuestion}>Photography and icon credits</span>
                    <span className={faq.triggerIcon} aria-hidden="true">
                      <Icon name="down" />
                    </span>
                  </button>
                  {creditsOpen && (
                    <div id="notice-panel-credits" className={faq.accordionBody}>
                      <ul
                        className={styles.creditList}
                        aria-label="Photography and icon attributions"
                      >
                        {assetAttributions.map((credit) => (
                          <li key={credit.label}>
                            <strong>
                              {credit.label} — {credit.author}
                            </strong>
                            <span>
                              <a href={credit.sourceUrl} target="_blank" rel="noreferrer">
                                {credit.sourceTitle}
                              </a>
                              {" · "}
                              <a href={credit.licenseUrl} target="_blank" rel="noreferrer">
                                {credit.license}
                              </a>
                              {" · "}
                              {credit.changes}
                            </span>
                          </li>
                        ))}
                      </ul>
                    </div>
                  )}
                </article>
              )}
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
