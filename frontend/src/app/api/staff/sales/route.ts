import { relaySaleRead, tableKeys } from "@/lib/sales/routes";
import { parseSalesTable } from "@/lib/sales/parse";

export function GET(request: Request) {
  return relaySaleRead(request, "", parseSalesTable, tableKeys);
}
