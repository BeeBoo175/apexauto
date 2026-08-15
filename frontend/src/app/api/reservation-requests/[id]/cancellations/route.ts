import { relayDepositRequestCancellation } from "@/lib/vehicles/deposit-request-cancellation-server";

export async function POST(request: Request, { params }: { params: Promise<{ id: string }> }) {
  return relayDepositRequestCancellation(request, (await params).id);
}
