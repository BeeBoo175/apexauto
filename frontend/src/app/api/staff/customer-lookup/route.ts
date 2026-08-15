import { hasIntakeAccess } from "@/lib/auth/actor";
import { relayGatedRead } from "@/lib/server/http/staff-relay";
import { parseCustomerLookup } from "@/lib/staff/assisted-customer";

export async function GET(request: Request) {
  return relayGatedRead(request, {
    allowed: hasIntakeAccess,
    denial: "Your role cannot act for a customer.",
    path: "/api/staff/customer-lookup",
    keys: ["search", "purpose"],
    parse: parseCustomerLookup,
  });
}
