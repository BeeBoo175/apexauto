import { parsePage } from "@/lib/api";
import { apiFetch } from "@/lib/api-browser";
import { parseBranch } from "@/lib/branches";

// Staff filters reuse confirmed choices; the public seller picker always requests current branches.
export function readBranchChoices(
  page: number,
  signal: AbortSignal,
  pageSize?: number,
  policy: { fresh?: boolean } = {},
) {
  const size = pageSize ? `&pageSize=${pageSize}` : "";
  return apiFetch(
    `/api/intake/branches?page=${page}${size}`,
    (value) => parsePage(value, parseBranch),
    {
      signal,
    },
    policy,
  );
}
