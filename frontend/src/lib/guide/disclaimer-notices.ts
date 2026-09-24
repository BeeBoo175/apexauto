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
    href: "/buying-guide#reserve",
    linkLabel: "Hold deposit rules",
  },
  {
    id: "indicative-calculators",
    icon: "file-text",
    title: "Financing and trade-in values are estimates",
    summary:
      "Repayment plans model a flat 3% annual rate over 1–9 years for budgeting, not a loan offer.",
    detail: "A trade-in price range is indicative until the branch inspection confirms the offer.",
    href: "/buying-guide#settle",
    linkLabel: "See a worked estimate",
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
    sourceUrl: "https://unsplash.com/photos/a-person-holding-a-car-key-in-their-hand-RL5ThSy5LR0",
    license: "Unsplash License",
    licenseUrl: "https://unsplash.com/license",
    changes: "Cropped",
  },
  {
    label: "Home hero showroom",
    author: "Erik Mclean",
    sourceTitle: "Pexels photograph",
    sourceUrl: "https://www.pexels.com/photo/modern-black-automobiles-in-car-showroom-7629179/",
    license: "Pexels License",
    licenseUrl: "https://www.pexels.com/license/",
    changes: "Dimmed behind copy",
  },
  {
    label: "Buying steps (home)",
    author: "Maria Sime and Antoni Shkraba",
    sourceTitle: "Unsplash and Pexels photographs",
    sourceUrl: "https://www.pexels.com/photo/man-and-woman-looking-at-a-car-4895448/",
    license: "Unsplash License and Pexels License",
    licenseUrl: "https://www.pexels.com/license/",
    changes: "Cropped",
  },
  {
    label: "How buying works",
    author: "silverkblack, Antoni Shkraba and Negative Space",
    sourceTitle: "Pexels photographs",
    sourceUrl:
      "https://www.pexels.com/photo/happy-couple-shopping-for-a-new-car-at-dealership-36729871/",
    license: "Pexels License",
    licenseUrl: "https://www.pexels.com/license/",
    changes: "Resized and cropped",
  },
  {
    label: "How selling works",
    author: "Gustavo Fring, WAVYVISUALS, Mario Amé and Antoni Shkraba",
    sourceTitle: "Pexels photographs",
    sourceUrl: "https://www.pexels.com/photo/man-checking-car-engine-4895455/",
    license: "Pexels License",
    licenseUrl: "https://www.pexels.com/license/",
    changes: "Resized and cropped",
  },
  {
    label: "About page workshop photograph",
    author: "Artem Podrez",
    sourceTitle: "Pexels photograph",
    sourceUrl: "https://www.pexels.com/photo/mechanic-standing-under-a-car-on-a-car-lift-8986119/",
    license: "Pexels License",
    licenseUrl: "https://www.pexels.com/license/",
    changes: "Resized to 1200px wide and cropped in the page",
  },
  {
    label: "About page team portraits",
    author: "Tony Zohari, Monirul Islam, Duy's House of Photo and Divya Modi",
    sourceTitle: "Pexels photographs",
    sourceUrl:
      "https://www.pexels.com/photo/professional-portrait-of-smiling-woman-in-studio-38197025/",
    license: "Pexels License",
    licenseUrl: "https://www.pexels.com/license/",
    changes: "Resized. The people pictured are stock-photo models, not ApexAuto staff",
  },
  {
    label: "Car photo placeholder",
    author: "Bulat843",
    sourceTitle: "Pexels photograph",
    sourceUrl:
      "https://www.pexels.com/photo/sleek-silver-sedan-parked-near-modern-building-34404246/",
    license: "Pexels License",
    licenseUrl: "https://www.pexels.com/license/",
    changes: "Cropped and resized",
  },
  {
    label: "Inspection cutaway and showroom illustrations",
    author: "ApexAuto",
    sourceTitle: "ApexAuto illustrations",
    sourceUrl: "/disclaimer",
    license: "Original ApexAuto assets",
    licenseUrl: "/disclaimer",
    changes: "Cropped and resized",
  },
  {
    label: "Warranty strip car",
    author: "ApexAuto",
    sourceTitle: "ApexAuto illustration",
    sourceUrl: "/disclaimer",
    license: "Original ApexAuto asset",
    licenseUrl: "/disclaimer",
    changes: "Trimmed to its edges and converted to WebP",
  },
  {
    label: "Demo car listing photographs",
    author:
      "Bindydad123, Automachi, Ethan Llamas, Alexander-93, Stephanemartin, Collard, Jakub Hałun, Andreas Eichler and Jacek Halicki",
    sourceTitle: "Wikimedia Commons files, one record each",
    sourceUrl: "https://commons.wikimedia.org/wiki/Category:Perodua_Myvi",
    license: "CC BY-SA 4.0, CC BY 3.0 or CC BY-SA 3.0",
    licenseUrl: "https://creativecommons.org/licenses/by-sa/4.0/",
    changes: "Wikimedia-generated 960px thumbnails, no local edits",
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
