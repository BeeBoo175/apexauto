import { SHOWROOM_CLOSES_AT, SHOWROOM_OPENS_AT } from "@/lib/guide/visit-terms";

// Contact details for this demonstration site. They are made up: the address uses the reserved .example domain and the
// number is not a working line.
export const SITE_CONTACT = {
  email: "hello@apexauto.example",
  phone: "03-0000 1234",
  phoneHref: "+60300001234",
  hours: `Monday to Saturday, ${SHOWROOM_OPENS_AT} to ${SHOWROOM_CLOSES_AT}`,
} as const;

export const LEGAL_UPDATED = "17 November 2026";
