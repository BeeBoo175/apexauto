import type { CurrentActor } from "@/lib/auth/actor";
import { isNonNegativeInteger, isRecord } from "@/lib/auth/contracts";
import { isPageSize } from "@/lib/pagination";
import { HttpError } from "./http";

export const API_DEADLINE_MS = 25_000;

export interface ApiConflict {
  code: string;
  message: string;
}

// Builds the conflict parser for a code-to-message map; unknown codes yield undefined.
export function codedConflicts(
  messages: Record<string, string>,
): (code: unknown) => ApiConflict | undefined {
  return (code) =>
    typeof code === "string" && Object.hasOwn(messages, code)
      ? { code, message: messages[code] }
      : undefined;
}

export interface Page<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  // Tab totals some staff tables return beside the page.
  counts?: Record<string, number>;
}

export interface ApiResult<T> {
  status: number;
  data: T | null;
  message: string;
  uncertain: boolean;
  actor?: CurrentActor;
  code?: string;
  retryAfterSeconds?: number;
}

export function apiReadError(
  message: string,
  ...results: (ApiResult<unknown> | null | undefined)[]
): HttpError {
  const limited = results.find((result) => result?.status === 429);
  return new HttpError(limited?.status ?? 502, message, limited?.retryAfterSeconds);
}

// A read that could not be confirmed: callers keep their last confirmed content and offer a retry.
export function unavailableResult<T>(message = ""): ApiResult<T> {
  return { status: 502, data: null, message, uncertain: false };
}

export type ApiRequest = <T>(
  path: string,
  parse: (value: unknown) => T | null,
  options?: RequestInit,
) => Promise<ApiResult<T>>;

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
  pageSize?: number,
): Page<T> | null {
  if (
    !isRecord(value) ||
    !Array.isArray(value.items) ||
    !isNonNegativeInteger(value.total) ||
    !isNonNegativeInteger(value.page) ||
    value.page < 1 ||
    typeof value.pageSize !== "number" ||
    (pageSize === undefined ? !isPageSize(value.pageSize) : value.pageSize !== pageSize) ||
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

export type CountedPage<T, K extends string> = Page<T> & { counts?: Record<K, number> };

// Tab totals ride beside the page; a response without them still parses and shows no badges.
export function parseCountedPage<T, K extends string>(
  value: unknown,
  parseItem: (item: unknown) => T | null,
  keys: readonly K[],
): CountedPage<T, K> | null {
  const page = parsePage(value, parseItem);
  if (!page || !isRecord(value)) {
    return null;
  }
  const raw = value.counts;
  if (raw === undefined || raw === null) {
    return page;
  }
  if (!isRecord(raw) || !keys.every((key) => isNonNegativeInteger(raw[key]))) {
    return null;
  }
  const counts = Object.fromEntries(keys.map((key) => [key, raw[key]])) as Record<K, number>;
  return { ...page, counts };
}
