import { parseWarranty } from "@/lib/settings/settings";
import {
  relaySettingsRead,
  relaySettingsWrite,
  shapeWarrantyUpdate,
} from "@/lib/settings/settings-routes";

export async function GET(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relaySettingsRead(request, "warranties", parseWarranty, id);
}

export async function PUT(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relaySettingsWrite(request, "warranties", "PUT", id, shapeWarrantyUpdate, parseWarranty);
}
