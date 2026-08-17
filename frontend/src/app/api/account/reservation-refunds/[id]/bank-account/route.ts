import { relayBankAccount } from "@/lib/vehicles/account-refunds-server";

export async function POST(request: Request, { params }: { params: Promise<{ id: string }> }) {
  return relayBankAccount(request, (await params).id);
}
