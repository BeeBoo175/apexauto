import { parseSaleReceipt } from "@/lib/sales/sale";
import { relaySaleCommand, shapeSaleCommand } from "@/lib/sales/sale-routes";

export async function POST(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relaySaleCommand(
    request,
    `/${id}/rejections`,
    shapeSaleCommand("rejections"),
    parseSaleReceipt,
  );
}
