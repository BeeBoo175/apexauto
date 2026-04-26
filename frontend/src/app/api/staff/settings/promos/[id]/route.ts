import { parsePromo } from "@/lib/settings/settings";
import {
  relaySettingsRead,
  relaySettingsWrite,
  shapePromoUpdate,
} from "@/lib/settings/settings-routes";

export async function GET(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relaySettingsRead(request, "promos", parsePromo, id);
}

export async function PUT(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relaySettingsWrite(request, "promos", "PUT", id, shapePromoUpdate, parsePromo);
}
