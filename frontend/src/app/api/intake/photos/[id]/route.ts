import { relayPhoto } from "@/lib/intake/routes";
import { parsePhoto } from "@/lib/intake/parse";
export async function PUT(request: Request, context: { params: Promise<{ id: string }> }) {
  return relayPhoto(request, (await context.params).id, parsePhoto);
}
