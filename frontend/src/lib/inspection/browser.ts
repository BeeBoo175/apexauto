import { apiFetch } from "@/lib/api-browser";

export function inspectionFetch<T>(
  path: string,
  parse: (value: unknown) => T | null,
  options: RequestInit = {},
  deadline = 22_000,
) {
  return apiFetch(`/api/staff/inspection/${path}`, parse, options, {
    deadlineMs: deadline,
    maximumResponseBytes: 1200 * 1024,
    requireActor: true,
  });
}
