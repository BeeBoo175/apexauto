import { formatCurrency } from "@/lib/currency-format";
import { RESERVATION_DEPOSIT, RESERVATION_HOLD_HOURS } from "@/lib/vehicles/deposit-terms";

export type FaqCategory =
  | "All"
  | "Buying & warranty"
  | "Inspection & grades"
  | "Selling & trade-in"
  | "Branches & test drives";

export interface FaqEntry {
  id: string;
  category: Exclude<FaqCategory, "All">;
  question: string;
  answer: string;
  href: string;
  linkLabel: string;
}

export const faqCategories: readonly FaqCategory[] = [
  "All",
  "Buying & warranty",
  "Inspection & grades",
  "Selling & trade-in",
  "Branches & test drives",
];

export const faqEntries: readonly FaqEntry[] = [
  {
    id: "otr-breakdown",
    category: "Buying & warranty",
    question: "What is included in the on-the-road (OTR) total?",
    answer:
      "Vehicle price, RM 500 processing fee, road tax, and 8% SST on the fee (RM 40), minus one promo discount. Insurance and ApexShield stay separate.",
    href: "/buying-guide#pricing",
    linkLabel: "Try OTR calculator",
  },
  {
    id: "reservation-hold",
    category: "Buying & warranty",
    question: `How does the ${formatCurrency(RESERVATION_DEPOSIT, "RM")} reservation hold work?`,
    answer: `It reserves the car for ${RESERVATION_HOLD_HOURS} hours and counts toward your cash down payment. Cancellations or expired holds trigger an immediate refund.`,
    href: "/buying-guide#pricing",
    linkLabel: "See hold rules",
  },
  {
    id: "promo-stacking",
    category: "Buying & warranty",
    question: "Can I use more than one promo code?",
    answer: "No. Only one active promo code applies per booking, capped at the vehicle price.",
    href: "/buying-guide#pricing",
    linkLabel: "Test promo discounts",
  },
  {
    id: "financing-rounding",
    category: "Buying & warranty",
    question: "Why can the final monthly installment differ by a few sen?",
    answer:
      "Monthly estimates use a 3% flat annual rate over 1–9 years rounded to exact sen, with any remainder settled in the final month.",
    href: "/buying-guide#financing",
    linkLabel: "Run financing estimator",
  },
  {
    id: "warranty-eligibility",
    category: "Buying & warranty",
    question: "Which ApexShield warranty plan matches each grade?",
    answer:
      "Grade A qualifies for Silver, Gold, or Platinum (up to 36m). Grade B qualifies for Gold (24m) or Silver (12m). Grade C qualifies for Silver.",
    href: "/buying-guide#warranty",
    linkLabel: "Compare ApexShield plans",
  },
  {
    id: "grades-meaning",
    category: "Inspection & grades",
    question: "How are Grade A, B, C, and Rejections decided?",
    answer:
      "Grades follow whichever limit is stricter: repair cost (< RM 500, RM 500–2,499.99, RM 2,500–15,000) or worst issue found (Advisory, Minor, Major, Critical).",
    href: "/inspection#grading",
    linkLabel: "Test grading rules",
  },
  {
    id: "repairs-before-sale",
    category: "Inspection & grades",
    question: "Are defects repaired before the car is sold?",
    answer:
      "Yes. Every Minor defect, Major defect, and paid repair item finishes workshop rectification before listing.",
    href: "/inspection#guarantees",
    linkLabel: "Read inspection promise",
  },
  {
    id: "puspakom-scope",
    category: "Inspection & grades",
    question: "Does ApexAuto's check replace a Puspakom or B5 inspection?",
    answer:
      "No. Our 210-point check covers dealership quality and stays separate from Malaysia's statutory Puspakom B5 transfer inspection.",
    href: "/disclaimer",
    linkLabel: "Read disclosures",
  },
  {
    id: "sell-requirements",
    category: "Selling & trade-in",
    question: "What do I need before booking a sell or trade-in slot?",
    answer:
      "Request an online valuation, upload four exterior angles, and pick a 90-minute branch bay at least two hours in advance.",
    href: "/sell",
    linkLabel: "Book a seller slot",
  },
  {
    id: "trade-in-lower-bound",
    category: "Selling & trade-in",
    question: "Why does the calculator apply the lower end of my trade-in quote?",
    answer:
      "Calculators preview the conservative lower bound until a 90-minute branch inspection confirms your final trade-in value.",
    href: "/buying-guide#trade-in",
    linkLabel: "Read trade-in steps",
  },
  {
    id: "branch-hours",
    category: "Branches & test drives",
    question: "Where are ApexAuto branches and when are they open?",
    answer:
      "Glenmarie, Petaling Jaya, and Bayan Lepas operate Monday to Saturday from 09:00 to 18:00 MYT.",
    href: "/branches",
    linkLabel: "View branch details",
  },
  {
    id: "test-drive-booking",
    category: "Branches & test drives",
    question: "Do test drives require a booking deposit?",
    answer:
      "No. You can book a 30-minute showroom test drive on any published car without a deposit.",
    href: "/vehicles",
    linkLabel: "Browse cars",
  },
];
