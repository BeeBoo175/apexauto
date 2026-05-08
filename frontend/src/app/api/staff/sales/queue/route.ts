import { queueKeys, relaySaleRead } from "@/lib/sales/routes";
import { parseSalesQueue } from "@/lib/sales/parse";

export function GET(request: Request) {
  return relaySaleRead(request, "/queue", parseSalesQueue, queueKeys);
}
