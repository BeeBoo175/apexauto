import { parseWarranties, parseWarranty } from "@/lib/settings/settings";
import {
  relaySettingsRead,
  relaySettingsWrite,
  shapeWarrantyCreate,
} from "@/lib/settings/settings-routes";

export function GET(request: Request) {
  return relaySettingsRead(request, "warranties", parseWarranties);
}

export function POST(request: Request) {
  return relaySettingsWrite(
    request,
    "warranties",
    "POST",
    null,
    shapeWarrantyCreate,
    parseWarranty,
  );
}
