import { parsePage } from "@/lib/api";
import { apiFetch } from "@/lib/api-browser";
import { parseBranch } from "@/lib/branches";

// Browser read of the branch choices every General Manager filter offers; pageSize is optional.
export function readBranchChoices(page: number, signal: AbortSignal, pageSize?: number) {
  const size = pageSize ? `&pageSize=${pageSize}` : "";
  return apiFetch(
    `/api/intake/branches?page=${page}${size}`,
    (value) => parsePage(value, parseBranch),
    {
      signal,
    },
  );
}
