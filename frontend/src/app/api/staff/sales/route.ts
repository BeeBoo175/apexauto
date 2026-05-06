import { relaySaleRead, tableKeys } from "@/lib/sales/sale-routes";
import { parseSalesTable } from "@/lib/sales/sales";

export function GET(request: Request) {
  return relaySaleRead(request, "", parseSalesTable, tableKeys);
}
