"use client";

import { useState } from "react";
import Link from "next/link";
import { Icon } from "@/components/ui/Icon";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import { dailyIntakeSlots } from "@/lib/guide/showrooms";
import styles from "./ShowroomSlotStrip.module.css";

export function ShowroomSlotStrip() {
  const [selectedId, setSelectedId] = useState(dailyIntakeSlots[0].id);
  const activeSlot = dailyIntakeSlots.find((slot) => slot.id === selectedId) ?? dailyIntakeSlots[0];

  return (
    <section className={styles.slotCard} aria-label="Seller appraisal bays and test-drive hours">
      <div className={styles.slotHeader}>
        <h2 className={styles.slotTitle}>Appraisal bays · 90 min</h2>
        <span className={styles.slotMeta}>2h advance notice</span>
      </div>

      <div className={styles.slotGrid} role="group" aria-label="Select seller appraisal window">
        {dailyIntakeSlots.map((slot) => (
          <button
            key={slot.id}
            type="button"
            className={styles.slotItem}
            aria-pressed={slot.id === selectedId}
            onClick={() => setSelectedId(slot.id)}
          >
            <span className={styles.slotTime}>{slot.windowLabel}</span>
            <span className={styles.slotCutoff}>{slot.cutoffLabel}</span>
          </button>
        ))}
      </div>

      <div className={styles.slotFooter}>
        <span className={styles.slotTestDriveNote}>Test drives run 30 min · no deposit</span>
        <Link href="/sell" prefetch={false} className={layout.arrowLink}>
          <span>Book {activeSlot.windowLabel} bay</span>
          <Icon name="next" />
        </Link>
      </div>
    </section>
  );
}
