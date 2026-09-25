import Image from "next/image";
import Link from "next/link";
import { buttonClassName } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { LiveVehicles } from "@/components/vehicles/LiveVehicles";
import { buyingPageFaq } from "@/lib/guide/faq-items";
import { GuideFaq } from "./GuideFaq";
import type { IconName } from "@/lib/icons";
import patterns from "@/components/layout/PublicPatterns.module.css";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import { formatCurrency, formatWholeCurrency } from "@/lib/currency-format";
import { PAYMENT_WINDOW_MINUTES, RESERVATION_DEPOSIT } from "@/lib/vehicles/deposit-terms";
import {
  calculateFinancing,
  DEFAULT_CASH_PERCENT,
  DEFAULT_TERM_YEARS,
  defaultCashContribution,
} from "@/lib/vehicles/financing";
import { handoverDocuments } from "@/lib/guide/handover-documents";
import { BuyingSteps, type BuyingStepPanel } from "./BuyingSteps";
import styles from "./BuyingJourney.module.css";

// An illustration, not a listed car, so the page needs no live data and can be generated ahead of time.
const EXAMPLE_ON_THE_ROAD = 60000;

// Photographs of models, never ApexAuto customers or listed cars; sources and licences are in public/images/README.md.
const STEP_PHOTOS = {
  choose: {
    src: "/images/guide-buy-choose.jpg",
    alt: "A smiling couple looks over a car together in a showroom.",
    position: "55% 40%",
  },
  reserve: {
    src: "/images/guide-buy-reserve.jpg",
    alt: "A customer shows something on her phone to a salesperson holding a folder, beside a car.",
    position: "50% 35%",
  },
  settle: {
    src: "/images/guide-buy-settle.jpg",
    alt: "A salesperson shakes hands with a customer while his partner smiles beside them.",
    position: "48% 40%",
  },
  collect: {
    src: "/images/guide-buy-keys.jpg",
    alt: "A hand holds out a car key.",
    position: "62% 45%",
  },
} as const;

const COST_LINES = [
  { label: "Vehicle price", note: "The listed price" },
  { label: "Processing fee", note: "A fixed fee" },
  { label: "Road tax", note: "Added at cost" },
  { label: "SST", note: "On the processing fee only" },
] as const;

function CostSheet() {
  return (
    <div className={patterns.sheet} data-fill>
      <div className={patterns.sheetHead}>
        <span className={patterns.sheetTitle}>What the on-the-road price is made of</span>
      </div>
      <ul className={`${patterns.sheetRows} ${styles.figureList}`}>
        {COST_LINES.map((line) => (
          <li key={line.label}>
            <span>{line.label}</span>
            <span className={styles.figureNote}>{line.note}</span>
          </li>
        ))}
        <li>
          <strong>On the road</strong>
          <span className={styles.figureNote}>The sum of the lines above</span>
        </li>
      </ul>
      <div className={patterns.sheetSign}>
        <span>
          Every car shows its exact figures. Insurance and optional warranty are excluded.
        </span>
        <Link href="/vehicles" prefetch={false} className={buttonClassName("primary", "sm")}>
          Browse the cars
        </Link>
      </div>
    </div>
  );
}

function EstimateTable() {
  const cash = Number(defaultCashContribution(EXAMPLE_ON_THE_ROAD));
  const estimate = calculateFinancing(EXAMPLE_ON_THE_ROAD, cash, DEFAULT_TERM_YEARS);
  if (!estimate) {
    return null;
  }

  const rows = [
    { label: "On the road", value: formatCurrency(EXAMPLE_ON_THE_ROAD) },
    { label: `Cash contribution (${DEFAULT_CASH_PERCENT}%)`, value: formatCurrency(cash) },
    { label: "Loan", value: formatCurrency(estimate.principal) },
    {
      label: `Monthly over ${DEFAULT_TERM_YEARS} years`,
      value: formatCurrency(estimate.monthlyInstallment),
    },
  ];

  return (
    <div className={patterns.sheet} data-fill>
      <div className={patterns.sheetHead}>
        <span className={patterns.sheetTitle}>Example · 3% flat rate</span>
      </div>
      <ul className={`${patterns.sheetRows} ${styles.figureList}`}>
        {rows.map((row) => (
          <li key={row.label}>
            <span>{row.label}</span>
            <span className={patterns.sheetResult}>{row.value}</span>
          </li>
        ))}
      </ul>
      <div className={patterns.sheetSign}>
        <span>An illustration, not a loan offer. Every car has its own calculator.</span>
        <Link href="/vehicles" prefetch={false} className={buttonClassName("primary", "sm")}>
          Browse the cars
        </Link>
      </div>
    </div>
  );
}

const PAYMENT_ROUTES: readonly { icon: IconName; title: string; detail: string }[] = [
  {
    icon: "receipt",
    title: "Cash or bank transfer",
    detail: "Our staff confirm the full balance before handover.",
  },
  {
    icon: "file-text",
    title: "Bank financing",
    detail: "A bank's letter of undertaking covers the loan, up to nine years.",
  },
  {
    icon: "car",
    title: "Trade in",
    detail: "An indicative allowance lowers the loan. It is not a confirmed offer.",
  },
];

function buildSteps(holdHours: number): BuyingStepPanel[] {
  return [
    {
      id: "choose",
      icon: "search",
      label: "Choose",
      photo: STEP_PHOTOS.choose,
      content: (
        <>
          <p className={patterns.lede}>
            Every price is listed up front. Only the processing fee carries SST, and road tax is
            added at cost.
          </p>
          <CostSheet />
        </>
      ),
    },
    {
      id: "reserve",
      icon: "clock",
      label: "Reserve",
      photo: STEP_PHOTOS.reserve,
      content: (
        <>
          <p className={patterns.lede}>
            A refundable {formatWholeCurrency(RESERVATION_DEPOSIT)} deposit takes the car off sale
            for {holdHours} hours and counts toward your cash contribution. Sign in with a verified
            email to reserve.
          </p>
          <ol className={styles.timeline} data-fill>
            <li>
              <strong>You reserve and pay</strong>
              <span>
                The car is held for {PAYMENT_WINDOW_MINUTES} minutes while you pay the deposit
                securely online.
              </span>
            </li>
            <li>
              <strong>The hold runs {holdHours} hours</strong>
              <span>Review the record, book a test drive and plan your payment.</span>
            </li>
            <li>
              <strong>Settle, or let it go</strong>
              <span>
                Cancel while the sale is still open, or let the hold lapse, and the full deposit is
                refunded.
              </span>
            </li>
          </ol>
        </>
      ),
    },
    {
      id: "settle",
      icon: "receipt",
      label: "Settle payment",
      photo: STEP_PHOTOS.settle,
      content: (
        <>
          <p className={patterns.lede}>
            Pay the balance in cash or by bank transfer, or finance it through a bank. The
            calculator on each car estimates the instalment from its on-the-road price.
          </p>
          <EstimateTable />
          <div className={styles.actions}>
            <Link href="/warranty" className={buttonClassName("secondary", "sm")}>
              ApexShield warranty
            </Link>
            <Link href="/selling-guide" className={buttonClassName("secondary", "sm")}>
              Trade in a car
            </Link>
          </div>
        </>
      ),
    },
    {
      id: "collect",
      icon: "steering-wheel",
      label: "Collect",
      photo: STEP_PHOTOS.collect,
      content: (
        <>
          <p className={patterns.lede}>
            Delivery is completed at the showroom that holds the car, where staff record the final
            odometer reading and hand over the keys. Bring these.
          </p>
          <div className={patterns.sheet} data-fill>
            <div className={patterns.sheetHead}>
              <span className={patterns.sheetTitle}>Bring on the day</span>
            </div>
            <ul className={patterns.sheetRows}>
              {handoverDocuments.map((document) => (
                <li key={document.title}>
                  <span className={patterns.sheetCode} aria-hidden="true">
                    <i className={patterns.tick} />
                  </span>
                  <span>
                    <strong>{document.title}</strong>
                    <br />
                    <small>{document.detail}</small>
                  </span>
                  <span />
                </li>
              ))}
            </ul>
            <div className={patterns.sheetSign}>
              <span>Find the showroom that holds your car.</span>
              <Link href="/branches" className={buttonClassName("primary", "sm")}>
                See the showrooms
              </Link>
            </div>
          </div>
        </>
      ),
    },
  ];
}

export function BuyingJourney({ holdHours }: { holdHours: number }) {
  return (
    <>
      <section className={patterns.hero} aria-labelledby="buying-guide-title">
        <Image
          src="/images/apexauto-detail.webp"
          alt=""
          fill
          priority
          sizes="100vw"
          className={patterns.heroPhoto}
        />
        <div className={patterns.heroScrim} aria-hidden="true" />
        <div className={`${layout.content} ${patterns.heroInner} ${patterns.rise}`}>
          <h1 id="buying-guide-title" className={layout.heroHeading}>
            How buying works
          </h1>
          <p className={patterns.heroLede}>
            Four steps from choosing a car to collecting it, with the costs and the monthly estimate
            worked through.
          </p>
          <Link href="/vehicles" prefetch={false} className={buttonClassName("primary", "lg")}>
            Browse the cars
          </Link>
        </div>
      </section>

      <section className={`${patterns.band} ${patterns.bandPaper}`} aria-label="The four steps">
        <div className={layout.content}>
          <BuyingSteps steps={buildSteps(holdHours)} />
        </div>
      </section>

      <section
        className={`${patterns.band} ${patterns.bandTone} ${styles.canvasBand}`}
        aria-label="Ways to pay and the reservation hold"
      >
        <div className={`${layout.content} ${styles.cards}`}>
          <div className={`${styles.payCard} ${patterns.riseOnView}`}>
            <div className={styles.payIntro}>
              <h2 className={layout.sectionHeading}>Pay your way</h2>
              <p className={patterns.lede}>
                Three ways to settle the balance once your car is reserved.
              </p>
            </div>
            <ul className={styles.routes}>
              {PAYMENT_ROUTES.map((route) => (
                <li key={route.title}>
                  <Icon name={route.icon} />
                  <strong>{route.title}</strong>
                  <span>{route.detail}</span>
                </li>
              ))}
            </ul>
          </div>

          <div className={`${styles.holdCard} ${patterns.riseOnView}`}>
            <div className={styles.holdPhoto}>
              <Image
                src="/images/guide-buy-reserve.jpg"
                alt=""
                fill
                quality={85}
                sizes="(max-width: 900px) 100vw, 420px"
                className={styles.holdImage}
              />
            </div>
            <div className={styles.holdCopy}>
              <h2 className={layout.sectionHeading}>Hold a car for {holdHours} hours</h2>
              <p className={patterns.lede}>
                A {formatWholeCurrency(RESERVATION_DEPOSIT)} deposit takes it off sale while you
                decide. Cancel while the sale is open, or let the hold lapse, and the full deposit
                is refunded.
              </p>
              <Link href="/vehicles" prefetch={false} className={buttonClassName("primary", "md")}>
                Find a car to reserve
              </Link>
            </div>
          </div>
        </div>
      </section>

      <section className={styles.carouselBand} aria-label="Cars on sale">
        <div className={layout.content}>
          <LiveVehicles title="Browse the cars on sale" />
        </div>
      </section>

      <section
        className={`${patterns.band} ${patterns.bandPaper}`}
        aria-label="Questions about buying"
      >
        <div className={layout.content}>
          <GuideFaq entries={buyingPageFaq(holdHours)} />
        </div>
      </section>
    </>
  );
}
