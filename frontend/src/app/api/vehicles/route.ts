import { apiJson, requestApiJson } from "@/lib/server/http/api";
import { parseCatalogue } from "@/lib/vehicles/parse";
import { catalogueParams, catalogueQuery } from "@/lib/vehicles/query";

export async function GET(request: Request) {
  const params = Object.fromEntries(new URL(request.url).searchParams);
  const { filters, page } = catalogueParams(params);
  return apiJson(
    await requestApiJson(`/api/vehicles?${catalogueQuery(filters, page)}`, parseCatalogue, {
      signal: request.signal,
    }),
  );
}
