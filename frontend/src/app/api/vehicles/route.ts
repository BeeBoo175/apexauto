import { apiJson, requestApiJson } from "@/lib/server/http/api";
import { CATALOGUE_LISTING_PAGE_SIZE, CATALOGUE_PAGE_SIZE } from "@/lib/vehicles/contracts";
import { parseCatalogue } from "@/lib/vehicles/parse";
import { catalogueParams, catalogueQuery } from "@/lib/vehicles/query";

export async function GET(request: Request) {
  const params = Object.fromEntries(new URL(request.url).searchParams);
  const { filters, page } = catalogueParams(params);
  // Only the catalogue page asks for the larger listing size; every other reader keeps twelve.
  const pageSize =
    params.pageSize === String(CATALOGUE_LISTING_PAGE_SIZE)
      ? CATALOGUE_LISTING_PAGE_SIZE
      : CATALOGUE_PAGE_SIZE;
  return apiJson(
    await requestApiJson(
      `/api/vehicles?${catalogueQuery(filters, page)}&pageSize=${pageSize}`,
      parseCatalogue,
      {
        signal: request.signal,
      },
    ),
  );
}
