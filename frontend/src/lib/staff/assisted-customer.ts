import { isRecord, isUuid, type CustomerAccount } from "@/lib/auth/contracts";
import { parseItems } from "@/lib/api";

// Staff who book for a customer name that customer in this header; the relays and the backend both read it.
export const ON_BEHALF_OF_HEADER = "X-On-Behalf-Of";

export const CUSTOMER_LOOKUP_MINIMUM = 2;

// What staff are booking, which decides who is offered: a customer has one active reservation and one outstanding
// test drive at most, while intake appointments have no such limit.
export type CustomerLookupPurpose = "intake" | "testDrive" | "reservation";

// The picker shows contact masked, exactly as the backend returns it.
export interface CustomerLookupRow {
  id: string;
  name: string;
  email: string;
  mobile: string;
}

export function assistedHeaders(customerId?: string): Record<string, string> {
  return customerId ? { [ON_BEHALF_OF_HEADER]: customerId } : {};
}

function parseRow(value: unknown): CustomerLookupRow | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    typeof value.name !== "string" ||
    typeof value.email !== "string" ||
    typeof value.mobile !== "string"
  ) {
    return null;
  }
  return { id: value.id, name: value.name, email: value.email, mobile: value.mobile };
}

export function parseCustomerLookup(value: unknown) {
  if (!isRecord(value)) {
    return null;
  }
  const items = parseItems(value.items, parseRow);
  return items && items.length <= 10 ? { items } : null;
}

// The customer lookup returns only verified accounts, so the account a staff member acts for is always verified.
export function assistedAccount(row: CustomerLookupRow): CustomerAccount {
  return {
    userId: row.id,
    legalName: row.name,
    email: row.email,
    emailVerified: true,
  };
}
