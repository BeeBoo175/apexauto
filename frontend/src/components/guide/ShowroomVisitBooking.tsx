"use client";

import Image from "next/image";
import Link from "next/link";
import { useMemo, useState } from "react";
import { buttonClassName } from "@/components/ui/Button";
import { useShowroomSlots } from "@/hooks/use-showroom-slots";
import {
  MALAYSIA_TIME_ZONE,
  addCalendarDays,
  calendarDate,
  calendarDayLabel,
  calendarTime,
} from "@/lib/calendar";
import type { ShowroomBranchInfo } from "@/lib/guide/showrooms";
import { BOOKING_NOTICE_LABEL } from "@/lib/guide/visit-terms";
import { isSundayDate } from "@/lib/intake/appointment-date";
import { sellerPrefillHref } from "@/lib/intake/appointment-prefill";
import type { SlotOffer } from "@/lib/intake/contracts";
import styles from "./ShowroomVisitBooking.module.css";

const DAYS_OFFERED = 7;
// The time chips drawn as placeholders while a day's bays load, so the row keeps its size.
const SLOT_SHELLS = [0, 1, 2, 3, 4, 5, 6, 7];

function offeredDays() {
  const days: string[] = [];
  let day = calendarDate();
  while (days.length < DAYS_OFFERED) {
    if (!isSundayDate(day)) {
      days.push(day);
    }
    day = addCalendarDays(day, 1);
  }
  return days;
}

// A chip names its day in two short lines, the weekday over the date, so seven days share one row with the times.
function dayChipLines(date: string) {
  const [year, month, day] = date.split("-").map(Number);
  const instant = new Date(Date.UTC(year, month - 1, day));
  return {
    weekday: instant.toLocaleDateString("en-MY", { weekday: "short", timeZone: "UTC" }),
    date: instant.toLocaleDateString("en-MY", { day: "numeric", month: "short", timeZone: "UTC" }),
  };
}

// An appraisal visit is booked here by picking a day and a time. A test drive is booked on the car itself.
export function ShowroomVisitBooking({ showroom }: { showroom: ShowroomBranchInfo }) {
  const days = useMemo(() => offeredDays(), []);
  const [date, setDate] = useState(days[0]);
  const [startsAt, setStartsAt] = useState("");
  const { offer, busy } = useShowroomSlots(showroom.id, date);
  // The times already on screen stay until the next day's times arrive, so choosing a day never empties the row.
  const [lastOffer, setLastOffer] = useState<SlotOffer | null>(null);
  if (offer && offer !== lastOffer) {
    setLastOffer(offer);
  }
  const shown = offer ?? (lastOffer?.branchId === showroom.id ? lastOffer : null);
  const shownDate = shown?.date ?? date;

  const slots = shown?.slots ?? [];
  const chosen = offer?.slots.find((slot) => slot.startsAt === startsAt) ?? null;
  const summary = chosen
    ? `${calendarDayLabel(shownDate)}, ${calendarTime(chosen.startsAt, MALAYSIA_TIME_ZONE)} at ${showroom.shortLabel}.`
    : `Pick a day and a time at ${showroom.shortLabel}.`;

  return (
    <div className={styles.visits}>
      <article className={styles.visit}>
        <div className={styles.photo}>
          <Image
            src="/images/sell-guide-visit.jpg"
            alt=""
            fill
            sizes="(max-width: 960px) 100vw, 480px"
          />
        </div>
        <div className={styles.body}>
          <div className={styles.header}>
            <h3>Sell or trade in your car</h3>
            <p className={styles.intro}>
              A technician inspects your car in a showroom bay and confirms the final offer on site.
            </p>
          </div>
          <fieldset>
            <legend>Day</legend>
            <div className={styles.days} role="group" aria-label="Day">
              {days.map((day) => (
                <button
                  key={day}
                  type="button"
                  aria-label={calendarDayLabel(day)}
                  aria-pressed={day === shownDate}
                  onClick={() => {
                    setDate(day);
                    setStartsAt("");
                  }}
                >
                  <span>{dayChipLines(day).weekday}</span>
                  <span>{dayChipLines(day).date}</span>
                </button>
              ))}
            </div>
          </fieldset>
          <fieldset>
            <legend>Time</legend>
            <div className={styles.slotArea} aria-busy={busy || !offer}>
              {!shown && (
                <div className={styles.slots} aria-hidden="true">
                  {SLOT_SHELLS.map((shell) => (
                    <span key={shell} className={styles.shellChip} data-shell />
                  ))}
                </div>
              )}
              {shown && slots.length > 0 && (
                <div className={styles.slots} role="group" aria-label="Time">
                  {slots.map((slot) => (
                    <button
                      key={slot.startsAt}
                      type="button"
                      aria-pressed={offer !== null && slot.startsAt === startsAt}
                      onClick={() => {
                        if (offer) {
                          setStartsAt(slot.startsAt);
                        }
                      }}
                    >
                      {calendarTime(slot.startsAt, MALAYSIA_TIME_ZONE)}
                    </button>
                  ))}
                </div>
              )}
              {shown && slots.length === 0 && (
                <p className={styles.empty}>No bays are left on this day. Try another day.</p>
              )}
            </div>
            <p className={styles.note}>
              Only bays still open are listed. Appraisals need at least {BOOKING_NOTICE_LABEL}&apos;
              notice.
            </p>
          </fieldset>
          <div className={styles.confirm}>
            <p aria-live="polite" className={styles.summary}>
              {summary}
            </p>
            <Link
              href={chosen ? sellerPrefillHref(showroom.id, chosen.startsAt) : "/sell"}
              className={buttonClassName(
                "primary",
                "lg",
                `${styles.action} ${chosen ? "" : styles.inactive}`,
              )}
              aria-disabled={!chosen}
              tabIndex={chosen ? undefined : -1}
            >
              Book appraisal
            </Link>
            <p className={styles.note}>
              Bring your registration card and keys. You sign in on the next step.
            </p>
          </div>
        </div>
      </article>
    </div>
  );
}
