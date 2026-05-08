import { parseSaleReceipt } from "@/lib/sales/parse";
import { relaySaleCommand, shapeSaleCommand } from "@/lib/sales/routes";

export async function POST(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relaySaleCommand(request, `/${id}/claims`, shapeSaleCommand("claims"), parseSaleReceipt);
}
