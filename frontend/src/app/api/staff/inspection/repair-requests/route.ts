import { relayInspection, inspectionQuery } from "@/lib/inspection/routes";
import { parseRepairPage } from "@/lib/inspection/parse";

export function GET(request: Request) {
  return relayInspection(request, `repair-requests?${inspectionQuery(request)}`, parseRepairPage);
}
