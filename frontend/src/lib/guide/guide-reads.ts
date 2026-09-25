import "server-only";

import { parsePage } from "@/lib/api";
import { parseBranch } from "@/lib/branches";
import { resolveShowrooms } from "@/lib/guide/showrooms";
import { parseInspectionGuide, type InspectionGuide } from "@/lib/guide/inspection-points";
import { parsePurchaseGuide, type PurchaseGuide } from "@/lib/guide/purchase-guide";
import { requestApiJson } from "@/lib/server/http/api";

// Owner's public content cache choice (audit PD-12): guide and showroom reads are reused for one hour.
// Pages that only read these export `revalidate` with the same literal, which Next requires.
export const PUBLIC_CONTENT_REVALIDATE_SECONDS = 3600;

const cached = { revalidateSeconds: PUBLIC_CONTENT_REVALIDATE_SECONDS };

export interface Guides {
  purchase: PurchaseGuide;
  inspection: InspectionGuide;
}

// Both guides, or the status of the one that failed.
export async function readGuides(): Promise<{ data: Guides | null; status: number }> {
  const [purchase, inspection] = await Promise.all([
    requestApiJson("/api/purchase-guide", parsePurchaseGuide, cached),
    requestApiJson("/api/inspection-guide", parseInspectionGuide, cached),
  ]);
  if (!purchase.data || !inspection.data) {
    const failed = purchase.data ? inspection : purchase;
    return { data: null, status: failed.status };
  }

  return { data: { purchase: purchase.data, inspection: inspection.data }, status: 200 };
}

export async function readShowrooms() {
  const result = await requestApiJson(
    "/api/branches?page=1&pageSize=25",
    (value) => parsePage(value, parseBranch),
    cached,
  );

  return { ...result, showrooms: resolveShowrooms(result.data?.items ?? []) };
}
