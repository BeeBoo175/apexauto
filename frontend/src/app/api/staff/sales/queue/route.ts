import { queueKeys, relaySaleRead } from "@/lib/sales/sale-routes";
import { parseSalesQueue } from "@/lib/sales/sales";

export function GET(request: Request) {
  return relaySaleRead(request, "/queue", parseSalesQueue, queueKeys);
}
