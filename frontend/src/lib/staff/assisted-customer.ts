import { isRecord, isUuid } from "@/lib/auth/contracts";
import { parseItems } from "@/lib/api";

// Staff who book for a customer name that customer in this header; the relays and the backend both read it.
export const ON_BEHALF_OF_HEADER = "X-On-Behalf-Of";

export const CUSTOMER_LOOKUP_MINIMUM = 2;

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
