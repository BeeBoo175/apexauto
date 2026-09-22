import { SITE_CONTACT } from "@/lib/site-contact";
import type { LegalSection } from "./legal-types";

export const PRIVACY_INTRO =
  "This policy explains what personal data the ApexAuto demonstration site collects, why, how long it is kept and what you can do about it. ApexAuto is a portfolio project that shows how a used car dealership could work online. It is not a real dealership, and this policy is written for that scope.";

export const privacySections: readonly LegalSection[] = [
  {
    heading: "Who this policy covers",
    paragraphs: [
      "This policy applies to visitors and customers who use the public pages of this site, and to customers who register an account. Staff accounts are separate and are not covered here.",
      "The site is operated by its owner as a demonstration under the name ApexAuto Sdn Bhd. No real vehicles are sold and no real payments are taken. We have written this policy with Malaysia's Personal Data Protection Act 2010 in mind.",
    ],
  },
  {
    heading: "Please use made-up details",
    paragraphs: [
      "Because this is a demonstration, you do not need to give real personal details to try it, and we ask you not to. Use fictional names, a fictional NRIC number and a fictional address. If you want to see real email delivery, use an email address that you control and are happy to use for a demonstration.",
    ],
  },
  {
    heading: "What we collect",
    paragraphs: ["When you register for a customer account we ask for:"],
    items: [
      "your name, email address, mobile number, NRIC number and residential address",
      "a username and a password, which is stored as a hash and never in readable form",
    ],
    after: [
      "When you use the site we also record what you do in it: cars you reserve, test drives and appraisal visits you book, trade-in details and photographs you upload, and the emails the site sends you about them.",
      "The site sets one session cookie, named apexauto_token, to keep you signed in. It is not readable by scripts on the page and is removed when you sign out. We do not use advertising or analytics cookies.",
      "To limit repeated sign-in attempts, the site may use the network address of the device you connect from. It is used for that and for nothing else.",
    ],
  },
  {
    heading: "How we use it",
    paragraphs: ["We use your data only to run the features of the demonstration:"],
    items: [
      "to create and secure your account and to verify your email address",
      "to record reservations, test drives, appraisal bookings and the documents that follow from them",
      "to send emails about those things, such as a verification link or a booking confirmation",
      "to let the right staff role, in the right branch, see the records they need and no more",
    ],
    after: ["We do not sell your data, share it for advertising or use it to profile you."],
  },
  {
    heading: "Who can see it",
    paragraphs: [
      "Customers can only see their own records. Staff accounts see records for their own branch and role, and each action they take is recorded against their name and branch. An administrator manages accounts but does not gain access to vehicle or sales work by doing so.",
      "When you press a directions link, your browser opens Google Maps in a new tab. Google then handles what you do there under its own policy. The showroom map on this site is drawn from local files and does not contact a map provider.",
      "Emails are handed to the mail service the demonstration is set up with, which may deliver them for real. An email that has been delivered stays in the inbox it was sent to, even if the demonstration is later reset.",
    ],
  },
  {
    heading: "How long we keep it",
    paragraphs: [
      "The site keeps different records for different lengths of time, and these periods are settings that an administrator can change:",
    ],
    items: [
      "sign-in and verification records, and the details of emails sent: 30 days by default",
      "staff invitations: 30 days by default",
      "business records such as reservations, sales and inspections: seven years by default",
    ],
    after: [
      "Because this is a demonstration, the owner may also clear all records at any time, for example when the demo data is reset. Do not rely on anything you enter being kept.",
    ],
  },
  {
    heading: "Your choices",
    paragraphs: [
      "You can close your own account from your account page. Closing it ends your access everywhere and removes your email address, username, mobile number and sign-in from your account. This cannot be undone, and you can register again later as a new customer.",
      "Some business records, such as a past reservation or sale, keep your name, and where a sale document needs them your NRIC number and address, for the business-evidence period (seven years by default). They are then removed.",
      `To ask what we hold about you, to correct it, or to ask us to remove it before you close your account, write to ${SITE_CONTACT.email}.`,
    ],
  },
  {
    heading: "Security",
    paragraphs: [
      "Passwords are hashed. The session cookie is marked so that scripts cannot read it. Access to records is limited by role and branch on the server, not only in the pages you see. No demonstration can promise perfect security, which is one more reason to use made-up details.",
    ],
  },
  {
    heading: "Children",
    paragraphs: [
      "The site is meant for adults who could buy or sell a car. It is not aimed at children, and we do not knowingly collect their data.",
    ],
  },
  {
    heading: "Changes to this policy",
    paragraphs: [
      "If we change this policy we will change the date at the top of the page. Using the site after a change means you accept the updated policy.",
    ],
  },
  {
    heading: "Contact",
    paragraphs: [
      `Questions about this policy can be sent to ${SITE_CONTACT.email}, or you can call ${SITE_CONTACT.phone} during ${SITE_CONTACT.hours.toLowerCase()}. These contact details are made up for the demonstration.`,
    ],
  },
];
