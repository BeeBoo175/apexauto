import { isUuid } from "@/lib/auth/contracts";
import { parseSaleReceipt } from "@/lib/sales/sale";
import { relaySaleCommand, saleCommands, shapeSaleCommand } from "@/lib/sales/sale-routes";
import { apiJson } from "@/lib/server/http/api";

export async function POST(
  request: Request,
  context: { params: Promise<{ id: string; command: string }> },
) {
  const { id, command } = await context.params;
  const known = saleCommands.find((item) => item === command);
  if (!known || !isUuid(id)) {
    return apiJson({
      status: 404,
      data: null,
      message: "This sale action is unavailable.",
      uncertain: false,
    });
  }
  return relaySaleCommand(request, `/${id}/${known}`, shapeSaleCommand(known), parseSaleReceipt);
}
