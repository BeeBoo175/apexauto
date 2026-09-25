import Image from "next/image";
import Link from "next/link";
import patterns from "@/components/layout/PublicPatterns.module.css";
import { buttonClassName } from "@/components/ui/Button";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import { AboutOdometer } from "./AboutOdometer";
import { Reveal } from "./Reveal";
import styles from "./AboutUs.module.css";

// Everything below is a fictional company history for this demonstration site, set as of December 2026.

// Each line is something the platform does, so the charter never promises more than the code enforces.
function buildCharter(holdHours: number) {
  return [
    {
      word: "A signed record",
      line: "A technician checks 210 points and signs the result. You can read it line by line on the listing.",
    },
    {
      word: "Cleared before listing",
      line: "Only cars the workshop has cleared for sale can be listed. Structural, flood and tampered cars are not sold.",
    },
    {
      word: "An itemised price",
      line: "Vehicle price, processing fee, road tax and service tax are shown before you reserve.",
    },
    {
      word: `A ${holdHours}-hour hold`,
      line: `Reserve a car online and it is held for you for ${holdHours} hours.`,
    },
    {
      word: "Named work",
      line: "Every inspection, repair and sale step is recorded against the person and branch that did it.",
    },
  ] as const;
}

type TimelineKind = "milestone" | "showroom" | "award";

const TIMELINE: readonly { year: string; line: string; kind: TimelineKind }[] = [
  {
    year: "2014",
    line: "The workshop opens in Shah Alam with two technicians and one ramp.",
    kind: "milestone",
  },
  {
    year: "2016",
    line: "The 1,000th car passes inspection, and the written record becomes standard.",
    kind: "milestone",
  },
  { year: "2018", line: "The Petaling Jaya showroom opens.", kind: "showroom" },
  {
    year: "2020",
    line: "ApexShield launches, with a warranty on every graded car.",
    kind: "milestone",
  },
  { year: "2021", line: "The Bayan Lepas showroom opens in Penang.", kind: "showroom" },
  {
    year: "2023",
    line: "Finalist, Malaysia Used Car Retailer of the Year.",
    kind: "award",
  },
  { year: "2024", line: "The 10,000th car is inspected.", kind: "milestone" },
  { year: "2024", line: "Klang Valley Used Car Dealer of the Year.", kind: "award" },
  { year: "2025", line: "Online holds and trade-in appraisals go live.", kind: "milestone" },
  { year: "2025", line: "Penang Customer Service Award, Automotive.", kind: "award" },
  { year: "2026", line: "Twelve years in, the 14,000th car is signed off.", kind: "milestone" },
];

const PEOPLE = [
  {
    name: "Aisyah Rahman",
    role: "Founder and managing director",
    line: "Opened the workshop with one ramp in 2014.",
    photo: "/images/people/aisyah-rahman.jpg",
  },
  {
    name: "Kamal Ariffin",
    role: "Chief inspector",
    line: "Signs off the inspection standard every car is held to.",
    photo: "/images/people/kamal-ariffin.jpg",
  },
  {
    name: "Daniel Lim",
    role: "Head of operations",
    line: "Runs the three showrooms day to day.",
    photo: "/images/people/daniel-lim.jpg",
  },
  {
    name: "Priya Nair",
    role: "Head of customer care",
    line: "Answers the questions customers send in.",
    photo: "/images/people/priya-nair.jpg",
  },
] as const;

const NOTES = [
  {
    quote:
      "The inspection record was longer than the car's manual. I read every line before I paid a sen.",
    who: "Hafizah M.",
    detail: "Bought a Honda City S, Glenmarie",
  },
  {
    quote:
      "They told me the range for my Myvi on the website and the final offer matched it at the showroom.",
    who: "Ravi K.",
    detail: "Sold a Perodua Myvi, Petaling Jaya",
  },
  {
    quote:
      "A scratch they missed was fixed and signed off within the week. Nobody argued about it.",
    who: "Tan Wei Ling",
    detail: "Bought a Mazda 3, Bayan Lepas",
  },
  {
    quote: "Every fee was on the page before I reserved. No surprises at collection.",
    who: "Amir S.",
    detail: "Bought a Proton Saga, Glenmarie",
  },
] as const;

function SectionHead({ id, title, lede }: { id: string; title: string; lede: string }) {
  return (
    <Reveal as="header" className={styles.sectionHead}>
      <h2 id={id} className={layout.sectionHeading}>
        {title}
      </h2>
      <p className={patterns.lede}>{lede}</p>
    </Reveal>
  );
}

export function AboutUs({ holdHours }: { holdHours: number }) {
  return (
    <>
      <div className={styles.top}>
        <section className={styles.hero} aria-labelledby="about-title">
          <div className={`${layout.content} ${styles.heroGrid}`}>
            <div className={`${styles.heroCopy} ${patterns.rise}`}>
              <h1 id="about-title">About ApexAuto</h1>
              <p className={styles.heroStatement}>
                We buy, inspect, repair and sell used cars from three showrooms in Selangor and
                Penang.
              </p>
              <p className={styles.heroNote}>
                Every car is sold with its inspection record, and every price is itemised.
              </p>
            </div>
            <div className={`${patterns.photo} ${styles.heroPhoto} ${patterns.rise}`}>
              <Image
                src="/images/about-workshop.jpg"
                alt="A mechanic checking the underside of a car raised on a workshop lift"
                fill
                priority
                sizes="(max-width: 960px) 100vw, 480px"
              />
            </div>
          </div>
        </section>
        <div className={`${layout.content} ${styles.barSlot}`}>
          <AboutOdometer />
        </div>
      </div>

      <section
        className={`${patterns.band} ${patterns.bandTone} ${styles.afterBar}`}
        aria-labelledby="about-values"
      >
        <div className={layout.content}>
          <SectionHead
            id="about-values"
            title="What we stand for"
            lede="Each line on the clipboard is something our systems do, not a slogan."
          />
          <div className={styles.desk}>
            <Reveal className={styles.clipboard}>
              <span className={styles.clip} aria-hidden="true" />
              <div className={styles.sheet}>
                <div className={styles.sheetHead}>
                  <span>Standard operating charter</span>
                  <span>Rev. 2014 – 2026</span>
                </div>
                <ul>
                  {buildCharter(holdHours).map((item, index) => (
                    <Reveal as="li" key={item.word} delay={index * 110}>
                      <span className={styles.check} aria-hidden="true">
                        <span className={patterns.tick} />
                      </span>
                      <div>
                        <strong>{item.word}</strong>
                        <span>{item.line}</span>
                      </div>
                    </Reveal>
                  ))}
                </ul>
                <div className={styles.signoff}>
                  <span className={styles.signLabel}>Signed</span>
                  <strong>ApexAuto Inspection Team</strong>
                </div>
              </div>
            </Reveal>
            <span className={styles.pencil} aria-hidden="true" />
          </div>
        </div>
      </section>

      <section
        className={`${patterns.band} ${patterns.bandPaper}`}
        aria-labelledby="about-timeline"
      >
        <div className={layout.content}>
          <SectionHead
            id="about-timeline"
            title="Along the way"
            lede="Twelve years, from one ramp to three showrooms."
          />
          <ul className={styles.legend} aria-label="Key to the timeline">
            <li data-kind="milestone">Milestone</li>
            <li data-kind="showroom">Showroom</li>
            <li data-kind="award">Award</li>
          </ul>
          <ol className={styles.timeline}>
            {TIMELINE.map((entry, index) => (
              <Reveal
                as="li"
                key={`${entry.year}-${entry.line}`}
                from={index % 2 === 0 ? "left" : "right"}
                className={styles.entry}
              >
                <span data-kind={entry.kind} className={styles.entryBody}>
                  <time dateTime={entry.year}>{entry.year}</time>
                  <p>
                    {entry.kind === "award" && <span className="sr-only">Award: </span>}
                    {entry.line}
                  </p>
                </span>
              </Reveal>
            ))}
          </ol>
        </div>
      </section>

      <section className={`${patterns.band} ${patterns.bandCanvas}`} aria-labelledby="about-people">
        <div className={layout.content}>
          <SectionHead
            id="about-people"
            title="The people"
            lede="The team that keeps the record."
          />
          <ul className={styles.people}>
            {PEOPLE.map((person, index) => (
              <Reveal as="li" key={person.name} delay={index * 120}>
                <div className={styles.portrait}>
                  <Image
                    src={person.photo}
                    alt={`Portrait of ${person.name}`}
                    fill
                    sizes="(max-width: 640px) 50vw, (max-width: 960px) 45vw, 300px"
                  />
                </div>
                <strong>{person.name}</strong>
                <span className={styles.role}>{person.role}</span>
                <p>{person.line}</p>
              </Reveal>
            ))}
          </ul>
        </div>
      </section>

      <section className={`${patterns.band} ${patterns.bandTone}`} aria-labelledby="about-notes">
        <div className={layout.content}>
          <SectionHead
            id="about-notes"
            title="Notes from customers"
            lede="What people wrote after buying or selling."
          />
          <ul className={styles.notes}>
            {NOTES.map((note, index) => (
              <Reveal as="li" key={note.who} from="drop" delay={index * 120}>
                <span className={styles.pin} aria-hidden="true" />
                <blockquote>{note.quote}</blockquote>
                <p>
                  <strong>{note.who}</strong>
                  <span>{note.detail}</span>
                </p>
              </Reveal>
            ))}
          </ul>
        </div>
      </section>

      <section className={`${patterns.band} ${patterns.bandPaper}`} aria-label="Next steps">
        <div className={`${layout.content} ${styles.closing}`}>
          <p className={styles.closingLine}>See it for yourself.</p>
          <div className={styles.closingActions}>
            <Link href="/vehicles" prefetch={false} className={buttonClassName("primary", "lg")}>
              Browse the cars
            </Link>
            <Link href="/branches" className={buttonClassName("secondary", "lg")}>
              Visit a showroom
            </Link>
          </div>
        </div>
      </section>
    </>
  );
}
