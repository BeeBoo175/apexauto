import { parseDeal } from "@/lib/deals/deals";
import { dealActions, relayDealWrite, shapeDealTransition } from "@/lib/deals/deal-routes";
import { isUuid } from "@/lib/auth/contracts";
import { apiJson } from "@/lib/server/http/api";

export async function POST(
  request: Request,
  context: { params: Promise<{ id: string; action: string }> },
) {
  const { id, action } = await context.params;
  const known = Object.hasOwn(dealActions, action)
    ? dealActions[action as keyof typeof dealActions]
    : null;
  if (!known || !isUuid(id)) {
    return apiJson({
      status: 404,
      data: null,
      message: "This deal action is unavailable.",
      uncertain: false,
    });
  }
  return relayDealWrite(
    request,
    `/${id}/${action}`,
    "POST",
    shapeDealTransition(known.reason),
    parseDeal,
  );
}
