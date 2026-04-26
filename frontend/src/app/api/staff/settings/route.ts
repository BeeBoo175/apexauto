import { parseSettings } from "@/lib/settings/settings";
import {
  relaySettingsRead,
  relaySettingsWrite,
  shapeSettings,
} from "@/lib/settings/settings-routes";

export function GET(request: Request) {
  return relaySettingsRead(request, "settings", parseSettings);
}

export function PUT(request: Request) {
  return relaySettingsWrite(request, "settings", "PUT", null, shapeSettings, parseSettings);
}
