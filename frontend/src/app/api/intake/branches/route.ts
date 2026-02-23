import { DEFAULT_PAGE_SIZE, paginationQuery } from "@/lib/pagination";
import { relayIntake } from "@/lib/intake/routes";
import { parseBranch } from "@/lib/branches";
import { parsePage } from "@/lib/api";
export function GET(request: Request) {
  const query = new URL(request.url).searchParams;
  const page = query.get("page") ?? "1";
  const pageSize = query.get("pageSize") ?? DEFAULT_PAGE_SIZE;
  return relayIntake(request, `/api/branches?${paginationQuery(page, pageSize)}`, (value) =>
    parsePage(value, parseBranch),
  );
}
