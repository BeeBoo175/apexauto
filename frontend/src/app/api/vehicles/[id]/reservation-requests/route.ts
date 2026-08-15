import { relayDepositRequest } from "@/lib/vehicles/deposit-request-server";

export async function POST(request: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return relayDepositRequest(request, id);
}
