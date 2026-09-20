import { formatCurrency } from "@/lib/currency-format";
import { SITE_CONTACT } from "@/lib/site-contact";
import { RESERVATION_DEPOSIT, RESERVATION_HOLD_HOURS } from "@/lib/vehicles/deposit-terms";
import type { LegalSection } from "./legal-types";

export const TERMS_INTRO =
  "These terms govern your use of the ApexAuto demonstration site. ApexAuto is a portfolio project that shows how a used car dealership could work online. By using the site you agree to them. If you do not agree, please do not use it.";

export const termsSections: readonly LegalSection[] = [
  {
    heading: "What this site is",
    paragraphs: [
      "ApexAuto is a demonstration. It is not a real dealership, nobody is offering the cars you see for sale, and nothing on the site is a contract, an offer or advice. The company, the showrooms, the staff and the cars are fictional or are used as illustration.",
      "The site works like a real one so that its features can be tried: browsing cars, reserving one, booking a test drive, booking a trade-in appraisal and following a sale through to handover.",
    ],
  },
  {
    heading: "Money and bookings are simulated",
    paragraphs: ["Nothing you do on the site costs real money or commits you to anything:"],
    items: [
      `a ${formatCurrency(RESERVATION_DEPOSIT, "RM")} reservation deposit and its ${RESERVATION_HOLD_HOURS}-hour hold are recorded inside the demonstration, and no card or bank account is charged`,
      "refunds for cancelled or expired holds are simulated in the same way",
      "monthly repayment figures use a flat 3% annual rate over one to nine years, for budgeting only, and are not a loan offer",
      "a trade-in price range is indicative, and the final figure is only ever confirmed at a showroom inspection",
      "ApexShield warranty plans describe a fictional product",
    ],
    after: [
      "Test drive and appraisal times are shown so that the booking flow can be tried. A time shown as free is not a promise that anyone will be there.",
    ],
  },
  {
    heading: "Your account",
    paragraphs: [
      "You may register one customer account. Please use made-up details, as the privacy policy asks, and keep your password to yourself. You are responsible for what is done with your account.",
      "We may suspend or close an account that is used to break these terms, to attack the site or to bother other people, and we may do so without notice. You can close your own account at any time from your account page.",
    ],
  },
  {
    heading: "How you may use the site",
    paragraphs: ["Please do not:"],
    items: [
      "enter real personal data about yourself or anyone else, including a real NRIC number",
      "upload photographs or files you do not have the right to share, or that are unlawful or offensive",
      "try to get into accounts, records or systems that are not yours, or to test the site's security without asking first",
      "use scripts or bots to scrape the site or to make large numbers of requests",
      "use the site to impersonate a real business or person",
    ],
    after: ["The site limits repeated requests and may block activity that looks like abuse."],
  },
  {
    heading: "Content and ownership",
    paragraphs: [
      "The design, text and code of the site belong to its owner. The photographs and icons come from other people, and their authors, sources and licences are listed on the disclaimers page. Those works remain under their own licences.",
      "If you upload a photograph or other file, you keep your rights in it. You allow the site to store it and to show it back to you and to the staff roles that need it for the booking it belongs to.",
    ],
  },
  {
    heading: "Links to other sites",
    paragraphs: [
      "Some pages link to other sites, such as Google Maps for directions. We do not control those sites and are not responsible for them.",
    ],
  },
  {
    heading: "No warranty and limited liability",
    paragraphs: [
      "The site is provided as it is, without any promise that it will be available, accurate or free of errors. Prices, grades, availability and every other figure are illustrative.",
      "To the extent the law allows, the owner is not liable for any loss that comes from using the site or from relying on what it shows. Nothing in these terms limits liability that cannot be limited by law.",
    ],
  },
  {
    heading: "Resetting and ending the demonstration",
    paragraphs: [
      "The owner may change, pause, reset or take down the site at any time. Records, including accounts and bookings, may be cleared when that happens, so do not depend on anything you enter being kept.",
    ],
  },
  {
    heading: "Changes to these terms",
    paragraphs: [
      "We may change these terms and will change the date at the top of the page when we do. Using the site after a change means you accept the updated terms.",
    ],
  },
  {
    heading: "Governing law",
    paragraphs: [
      "These terms are governed by the laws of Malaysia, and the courts of Malaysia have jurisdiction over any dispute about them.",
    ],
  },
  {
    heading: "Contact",
    paragraphs: [
      `Questions about these terms can be sent to ${SITE_CONTACT.email}, or you can call ${SITE_CONTACT.phone} during ${SITE_CONTACT.hours.toLowerCase()}. These contact details are made up for the demonstration.`,
    ],
  },
];
