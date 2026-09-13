import { parseRetentionSettings } from "@/lib/settings/retention";
import {
  relaySettingsRead,
  relaySettingsWrite,
  shapeRetention,
} from "@/lib/settings/settings-routes";

export function GET(request: Request) {
  return relaySettingsRead(request, "retention", parseRetentionSettings);
}

export function PUT(request: Request) {
  return relaySettingsWrite(
    request,
    "retention",
    "PUT",
    null,
    shapeRetention,
    parseRetentionSettings,
  );
}
