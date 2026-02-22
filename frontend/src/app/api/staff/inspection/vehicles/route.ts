import { relayInspection, inspectionQuery } from "@/lib/inspection/routes";
import { parseInspectionPage } from "@/lib/inspection/parse";

export function GET(request: Request) {
  return relayInspection(request, `vehicles?${inspectionQuery(request)}`, parseInspectionPage);
}
