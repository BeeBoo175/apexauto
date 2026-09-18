import { formatCurrency } from "@/lib/currency-format";
import type { InspectionGradingPolicy } from "./inspection-points";
import { guideServiceTax, sstPercentLabel, type PurchaseGuide } from "./purchase-guide";
import { eligibleWarrantyLabel } from "./warranty-plans";
import {
  PAYMENT_WINDOW_MINUTES,
  RESERVATION_DEPOSIT,
  RESERVATION_HOLD_HOURS,
} from "@/lib/vehicles/deposit-terms";

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
  // Shown on the ApexShield warranty page too, so both pages give one answer.
  onWarrantyPage?: boolean;
  // Shown on the buying and selling guides too.
  onBuyingPage?: boolean;
  onSellingPage?: boolean;
}

export const faqCategories: readonly FaqCategory[] = [
  "All",
  "Buying & warranty",
  "Inspection & grades",
  "Selling & trade-in",
  "Branches & test drives",
];

// Entries that need no live figures, so the two guides can show them on pages generated ahead of time.
const buyingEntries: FaqEntry[] = [
  {
    id: "reserve-account",
    onBuyingPage: true,
    category: "Buying & warranty",
    question: "Do I need an account to reserve a car?",
    answer:
      "Yes. Create an account and verify your email, then reserve or book a test drive from the car's page.",
    href: "/register",
    linkLabel: "Create an account",
  },
  {
    id: "reservation-hold",
    onBuyingPage: true,
    category: "Buying & warranty",
    question: `How does the ${formatCurrency(RESERVATION_DEPOSIT, "RM")} reservation hold work?`,
    answer: `Once you start a reservation the car is held for ${PAYMENT_WINDOW_MINUTES} minutes while you pay securely online. After your payment is confirmed the car is held for ${RESERVATION_HOLD_HOURS} hours, and the deposit counts toward your cash down payment. If you cancel or the hold lapses, the deposit is refunded.`,
    href: "/buying-guide#reserve",
    linkLabel: "See how reserving works",
  },
  {
    id: "payment-window",
    onBuyingPage: true,
    category: "Buying & warranty",
    question: `What happens if I do not pay within ${PAYMENT_WINDOW_MINUTES} minutes?`,
    answer: `The reservation is made only once payment is confirmed. If the ${PAYMENT_WINDOW_MINUTES}-minute payment window runs out, the car is released and nothing is charged. You can start again while the car is still listed.`,
    href: "/vehicles",
    linkLabel: "Browse cars",
  },
  {
    id: "promo-codes",
    category: "Buying & warranty",
    question: "How do I use a promo code?",
    answer:
      "Open the car and choose Reserve, then open the promo code field, enter the code exactly as given (codes are case-sensitive) and press Apply. The saving appears as its own line before you pay. Only one code applies to a booking, it is capped at the vehicle price, and it does not reserve the car or lock its price.",
    href: "/vehicles",
    linkLabel: "Browse cars",
  },
  {
    id: "financing-rounding",
    onBuyingPage: true,
    category: "Buying & warranty",
    question: "How is the monthly installment estimated?",
    answer:
      "Each car's Financing tab uses a 3% flat annual rate over 1–9 years, rounded to exact sen with any remainder settled in the final month. It is a budgeting estimate, not a loan offer.",
    href: "/buying-guide#settle",
    linkLabel: "See a worked estimate",
  },
];

const sellingEntries: FaqEntry[] = [
  {
    id: "sell-requirements",
    onSellingPage: true,
    category: "Selling & trade-in",
    question: "What do I need before booking a sell or trade-in slot?",
    answer:
      "Get an online price range, sign in, upload four exterior photos, then pick a 90-minute showroom bay at least two hours ahead and within the next 30 days.",
    href: "/sell",
    linkLabel: "Start selling",
  },
  {
    id: "trade-in-offer",
    onSellingPage: true,
    category: "Selling & trade-in",
    question: "Is the online price range an offer?",
    answer:
      "No. It is an indicative range from your make, model, year and mileage. The final offer is confirmed on site after the 90-minute bay inspection.",
    href: "/selling-guide",
    linkLabel: "See how selling works",
  },
];

export const buyingPageFaq = buyingEntries.filter((entry) => entry.onBuyingPage);
export const sellingPageFaq = sellingEntries.filter((entry) => entry.onSellingPage);

export function faqEntries(
  guide: PurchaseGuide,
  grading: InspectionGradingPolicy,
): readonly FaqEntry[] {
  const shortestCover = Math.min(...guide.warranties.map((plan) => plan.durationMonths));
  const longestCover = Math.max(...guide.warranties.map((plan) => plan.durationMonths));

  return [
    {
      id: "otr-breakdown",
      category: "Buying & warranty",
      question: "What is included in the on-the-road (OTR) total?",
      answer: `Vehicle price, ${formatCurrency(guide.processingFee, "RM")} processing fee, road tax, and ${sstPercentLabel(guide.sstRate)} SST on the fee (${formatCurrency(guideServiceTax(guide), "RM")}), minus one promo discount. Insurance and ApexShield stay separate.`,
      href: "/buying-guide#choose",
      linkLabel: "See a worked example",
      onWarrantyPage: true,
    },
    ...buyingEntries,
    {
      id: "warranty-included",
      category: "Buying & warranty",
      question: "Is the warranty included in the car's price?",
      answer:
        "No. ApexShield is optional cover, billed separately from the on-the-road total, so you choose it only if you want it.",
      href: "/warranty",
      linkLabel: "Compare ApexShield plans",
      onWarrantyPage: true,
    },
    {
      id: "warranty-eligibility",
      category: "Buying & warranty",
      question: "Which ApexShield warranty plan matches each grade?",
      answer: `Grade A qualifies for ${eligibleWarrantyLabel(guide.warranties, "A")}. Grade B qualifies for ${eligibleWarrantyLabel(guide.warranties, "B")}. Grade C qualifies for ${eligibleWarrantyLabel(guide.warranties, "C")}.`,
      href: "/warranty",
      linkLabel: "Compare ApexShield plans",
      onWarrantyPage: true,
    },
    {
      id: "warranty-duration",
      category: "Buying & warranty",
      question: "How long does ApexShield cover last?",
      answer: `Between ${shortestCover} and ${longestCover} months, depending on the plan, up to the mileage limit shown for it.`,
      href: "/warranty",
      linkLabel: "See plan terms",
      onWarrantyPage: true,
    },
    {
      id: "grades-meaning",
      category: "Inspection & grades",
      question: "How are Grade A, B, C, and Rejections decided?",
      answer: `Grades follow whichever limit is stricter: repair cost (< ${formatCurrency(grading.gradeBMinimumRepairCost, "RM")}, ${formatCurrency(grading.gradeBMinimumRepairCost, "RM")}–${formatCurrency(grading.gradeCMinimumRepairCost - 0.01, "RM")}, ${formatCurrency(grading.gradeCMinimumRepairCost, "RM")}–${formatCurrency(grading.maximumRepairCost, "RM")}) or worst issue found (Advisory, Minor, Major, Critical).`,
      href: "/inspection#grades",
      linkLabel: "Try the grade ruler",
      onWarrantyPage: true,
    },
    {
      id: "repairs-before-sale",
      category: "Inspection & grades",
      question: "Are defects repaired before the car is sold?",
      answer:
        "Yes. Every Minor defect, Major defect, and paid repair item finishes workshop rectification before listing.",
      href: "/inspection#repairs",
      linkLabel: "See repairs before listing",
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
    ...sellingEntries,
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
        "No. Verified customers can open any listed car and book a 30-minute slot. A test drive does not reserve the car, so no deposit is taken.",
      href: "/vehicles",
      linkLabel: "Browse cars",
    },
  ];
}
