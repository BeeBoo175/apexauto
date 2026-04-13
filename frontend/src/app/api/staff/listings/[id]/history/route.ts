import { relayListingRead } from "@/lib/vehicles/listing-routes";
import { parseListingHistory } from "@/lib/vehicles/listing-history";

export async function GET(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  const source = new URL(request.url).searchParams;
  const query = new URLSearchParams();
  for (const key of ["page", "pageSize"]) {
    if (source.has(key)) {
      query.set(key, source.get(key)!);
    }
  }
  return relayListingRead(
    request,
    id,
    `/history?${query}`,
    parseListingHistory,
    (value) => value.vehicleId === id,
  );
}
