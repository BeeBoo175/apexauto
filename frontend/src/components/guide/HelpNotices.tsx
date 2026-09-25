"use client";

import { useState, useSyncExternalStore } from "react";
import Link from "next/link";
import patterns from "@/components/layout/PublicPatterns.module.css";
import { Icon } from "@/components/ui/Icon";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import { assetAttributions, disclaimerNotices } from "@/lib/guide/disclaimer-notices";
import type { IconName } from "@/lib/icons";
import { GuideAccordionList, type GuideAccordionRow } from "./GuideAccordionList";
import styles from "./HelpNotices.module.css";

type NoticeTopic = "All" | "Notices" | "Credits";

const topicDefinitions: readonly { id: NoticeTopic; icon: IconName }[] = [
  { id: "All", icon: "help" },
  { id: "Notices", icon: "shield-check" },
  { id: "Credits", icon: "file-text" },
];

const CREDITS_ID = "credits";

function subscribeToHash(onChange: () => void) {
  window.addEventListener("hashchange", onChange);
  return () => window.removeEventListener("hashchange", onChange);
}

function countFor(name: NoticeTopic) {
  if (name === "Notices") {
    return disclaimerNotices.length;
  }
  return name === "Credits" ? 1 : disclaimerNotices.length + 1;
}

const topics = topicDefinitions.map((definition) => ({
  id: definition.id,
  label: definition.id,
  icon: definition.icon,
  count: countFor(definition.id),
}));

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

  function toggle(id: string) {
    setOpenIds((current) =>
      current.includes(id) ? current.filter((item) => item !== id) : [...current, id],
    );
  }

  const noticeRows: GuideAccordionRow[] = showNotices
    ? disclaimerNotices.map((notice) => ({
        id: notice.id,
        anchorId: notice.id,
        panelId: `notice-panel-${notice.id}`,
        title: notice.title,
        expanded: openIds.includes(notice.id),
        onToggle: () => toggle(notice.id),
        body: (
          <>
            <p>
              {notice.summary} {notice.detail}
            </p>
            <Link href={notice.href} prefetch={false} className={layout.arrowLink}>
              <span>{notice.linkLabel}</span>
              <Icon name="next" />
            </Link>
          </>
        ),
      }))
    : [];

  const creditsRow: GuideAccordionRow = {
    id: CREDITS_ID,
    anchorId: CREDITS_ID,
    panelId: "notice-panel-credits",
    title: "Photography and icon credits",
    expanded: creditsOpen,
    onToggle: () => setCreditsChoice(!creditsOpen),
    body: (
      <ul className={styles.creditList} aria-label="Photography and icon attributions">
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
    ),
  };

  const rows = showCredits ? [...noticeRows, creditsRow] : noticeRows;

  return (
    <section
      id="notices"
      className={`${patterns.band} ${patterns.bandCanvas}`}
      aria-labelledby="notices-heading"
    >
      <div className={layout.content}>
        <GuideAccordionList
          railLabel="Notice topics"
          filterLabel="Filter notice topics"
          topics={topics}
          activeTopic={topic}
          onTopicChange={setTopic}
          title={topic === "All" ? "All notices" : topic}
          titleId="notices-heading"
          rows={rows}
        />
      </div>
    </section>
  );
}
