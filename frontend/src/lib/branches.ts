import { isRecord, isUuid } from "@/lib/auth/contracts";

export interface Branch {
  id: string;
  name: string;
  address: string;
  city: string;
  state: string;
  phone: string;
}

const text = (value: unknown): value is string => typeof value === "string" && value.length > 0;

export function parseBranch(value: unknown): Branch | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !text(value.name) ||
    !text(value.address) ||
    !text(value.city) ||
    !text(value.state) ||
    !text(value.phone)
  ) {
    return null;
  }
  return {
    id: value.id,
    name: value.name,
    address: value.address,
    city: value.city,
    state: value.state,
    phone: value.phone,
  };
}
