import { parseSaleVersion } from "@/lib/sales/sale-history";
import { relaySaleRead } from "@/lib/sales/sale-routes";

export async function GET(
  request: Request,
  context: { params: Promise<{ id: string; version: string }> },
) {
  const { id, version } = await context.params;
  return relaySaleRead(request, `/${id}/versions/${version}`, parseSaleVersion);
}
