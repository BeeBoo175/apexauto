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

export function showroomName(name: string) {
  return name.replace(/^ApexAuto\s+/i, "");
}

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

// Every branch is named "ApexAuto Bayan Lepas" and so on; on a page that is all ApexAuto the shared first word only
// repeats, so labels show the place.
export function branchLabel(name: string) {
  return name.replace(/^ApexAuto\s+/, "");
}
