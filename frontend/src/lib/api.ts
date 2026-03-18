import type { CurrentActor } from "@/lib/auth/actor";
import { isRecord } from "@/lib/auth/contracts";
import { isPageSize } from "@/lib/pagination";

export const API_DEADLINE_MS = 25_000;

export const apiErrorMessages = {
  "check-in-content-conflict":
    "This appointment was checked in with different details. Refresh to review the recorded check-in.",
  "check-in-unavailable":
    "This appointment or its evidence is unavailable for check-in. Refresh the appointment before continuing.",
  "vehicle-identity-conflict":
    "The VIN or registration already identifies a vehicle. Review the vehicle identity before continuing.",
};

export type ApiErrorCode = keyof typeof apiErrorMessages;

export function parseApiErrorCode(value: unknown): ApiErrorCode | undefined {
  if (typeof value !== "string" || !Object.hasOwn(apiErrorMessages, value)) {
    return undefined;
  }
  return value as ApiErrorCode;
}

export interface Page<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface ApiResult<T> {
  status: number;
  data: T | null;
  message: string;
  uncertain: boolean;
  actor?: CurrentActor;
  code?: ApiErrorCode;
  retryAfterSeconds?: number;
}

export type ApiRequest = <T>(
  path: string,
  parse: (value: unknown) => T | null,
  options?: RequestInit,
) => Promise<ApiResult<T>>;

const integer = (value: unknown): value is number =>
  Number.isSafeInteger(value) && Number(value) >= 0;

export function parseItems<T>(value: unknown, parse: (item: unknown) => T | null): T[] | null {
  if (!Array.isArray(value)) {
    return null;
  }
  const items: T[] = [];
  for (const item of value) {
    const parsed = parse(item);
    if (parsed === null) {
      return null;
    }
    items.push(parsed);
  }
  return items;
}

export function parsePage<T>(
  value: unknown,
  parseItem: (item: unknown) => T | null,
): Page<T> | null {
  if (
    !isRecord(value) ||
    !Array.isArray(value.items) ||
    !integer(value.total) ||
    !integer(value.page) ||
    value.page < 1 ||
    !isPageSize(value.pageSize) ||
    value.items.length > value.pageSize
  ) {
    return null;
  }
  const items = parseItems(value.items, parseItem);
  if (!items || items.length > value.total) {
    return null;
  }
  return { items, total: value.total, page: value.page, pageSize: value.pageSize };
}
