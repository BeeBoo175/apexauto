import { relaySalesRead } from "@/lib/sales/sales-routes";
import { parseSalesQueue } from "@/lib/sales/sales";

export function GET(request: Request) {
  return relaySalesRead(request, "/sales/queue", parseSalesQueue, ["view", "page", "pageSize"]);
}
