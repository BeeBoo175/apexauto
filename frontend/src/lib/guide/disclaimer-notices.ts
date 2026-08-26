import { formatCurrency } from "@/lib/currency-format";
import { RESERVATION_DEPOSIT, RESERVATION_HOLD_HOURS } from "@/lib/vehicles/deposit-terms";

import type { IconName } from "@/lib/icons";

export interface AssetAttributionItem {
  label: string;
  author: string;
  sourceTitle: string;
  sourceUrl: string;
  license: string;
  licenseUrl: string;
  changes: string;
}

export interface DisclaimerNotice {
  id: string;
  icon: IconName;
  title: string;
  summary: string;
  detail: string;
  href: string;
  linkLabel: string;
}

export const disclaimerNotices: readonly DisclaimerNotice[] = [
  {
    id: "simulated-payments",
    icon: "shield-check",
    title: `${formatCurrency(RESERVATION_DEPOSIT, "RM")} holds and refunds are simulated`,
    summary: `Reservations record a ${RESERVATION_HOLD_HOURS}-hour hold inside the demo without charging a card or bank account.`,
    detail: "Cancelled or expired holds return an instant simulated refund.",
    href: "/buying-guide#pricing",
    linkLabel: "Hold deposit rules",
  },
  {
    id: "indicative-calculators",
    icon: "file-text",
    title: "Financing and trade-in values are estimates",
    summary:
      "Repayment plans model a flat 3% annual rate over 1–9 years for budgeting, not a loan offer.",
    detail: "Trade-in credit uses your range lower bound until branch inspection.",
    href: "/buying-guide#financing",
    linkLabel: "Financing calculator",
  },
  {
    id: "inspection-scope",
    icon: "inspection",
    title: "210-point inspection vs statutory Puspakom B5",
    summary:
      "Our checklist determines ApexAuto Grades A, B, and C, excluding optional gear marked Not fitted.",
    detail: "Mandatory Puspakom and B5 checks still apply before ownership transfer.",
    href: "/inspection",
    linkLabel: "Inspection rules",
  },
  {
    id: "accounts-fixtures",
    icon: "users",
    title: "Sample branches and isolated accounts",
    summary:
      "Glenmarie, Petaling Jaya, and Bayan Lepas schedules demonstrate Malaysian showroom workflows.",
    detail: "Buyer accounts stay separated from internal staff workspaces.",
    href: "/branches",
    linkLabel: "Branch schedule",
  },
];

export const assetAttributions: readonly AssetAttributionItem[] = [
  {
    label: "Engine bay",
    author: "Raiko",
    sourceTitle: "Wikimedia Commons file",
    sourceUrl: "https://commons.wikimedia.org/wiki/File:LEXUS_IS_F_%2711MY_Engine_Compartment.jpg",
    license: "CC BY-SA 3.0",
    licenseUrl: "https://creativecommons.org/licenses/by-sa/3.0/",
    changes: "Resized and framed",
  },
  {
    label: "Exterior paint & lamp",
    author: "Dietmar Rabich",
    sourceTitle: "Wikimedia Commons file",
    sourceUrl:
      "https://commons.wikimedia.org/wiki/File:D%C3%BClmen,_Auto_Bertels,_Toyota_GR_Supra_--_2021_--_9564.jpg",
    license: "CC BY-SA 4.0",
    licenseUrl: "https://creativecommons.org/licenses/by-sa/4.0/",
    changes: "Resized and framed",
  },
  {
    label: "Interior cockpit",
    author: "Acabashi",
    sourceTitle: "Wikimedia Commons file",
    sourceUrl:
      "https://commons.wikimedia.org/wiki/File:2020_DS_7_Crossback_cockpit_interior_in_Chingford,_London.jpg",
    license: "CC BY-SA 4.0",
    licenseUrl: "https://creativecommons.org/licenses/by-sa/4.0/",
    changes: "Resized and framed",
  },
  {
    label: "Brake rotor & wheel",
    author: "InSapphoWeTrust",
    sourceTitle: "Wikimedia Commons file",
    sourceUrl:
      "https://commons.wikimedia.org/wiki/File:Porsche_Panamera_Turbo_S_(US)_(6361618809).jpg",
    license: "CC BY-SA 2.0",
    licenseUrl: "https://creativecommons.org/licenses/by-sa/2.0/",
    changes: "Resized and framed",
  },
  {
    label: "Hero sedan & studio grille",
    author: "Campbell & studio archive",
    sourceTitle: "Unsplash photography",
    sourceUrl: "https://unsplash.com/photos/c6T2CctPSP0",
    license: "Unsplash License",
    licenseUrl: "https://unsplash.com/license",
    changes: "Cropped for storefront",
  },
  {
    label: "Key handover",
    author: "Ivan Kazlouskij",
    sourceTitle: "Unsplash photograph",
    sourceUrl: "https://unsplash.com/photos/a-person-holding-a-car-key-in-their-hand-V1P1J5G_xK8",
    license: "Unsplash License",
    licenseUrl: "https://unsplash.com/license",
    changes: "Cropped",
  },
  {
    label: "UI outline icons",
    author: "Tabler Icons & Bootstrap Icons",
    sourceTitle: "Tabler Icons repository",
    sourceUrl: "https://tabler.io/icons",
    license: "MIT License",
    licenseUrl: "https://opensource.org/license/mit",
    changes: "Unmodified",
  },
  {
    label: "Peninsular map vectors",
    author: "OpenStreetMap contributors via geoBoundaries",
    sourceTitle: "geoBoundaries ADM1 dataset",
    sourceUrl: "https://www.geoboundaries.org/api/current/gbOpen/MYS/ADM1/",
    license: "ODbL 1.0",
    licenseUrl: "https://opendatacommons.org/licenses/odbl/1-0/",
    changes: "Projected for Peninsular view",
  },
];
