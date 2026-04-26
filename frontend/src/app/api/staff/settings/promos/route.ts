import { parsePromo, parsePromos } from "@/lib/settings/settings";
import {
  relaySettingsRead,
  relaySettingsWrite,
  shapePromoCreate,
} from "@/lib/settings/settings-routes";

export function GET(request: Request) {
  return relaySettingsRead(request, "promos", parsePromos);
}

export function POST(request: Request) {
  return relaySettingsWrite(request, "promos", "POST", null, shapePromoCreate, parsePromo);
}
