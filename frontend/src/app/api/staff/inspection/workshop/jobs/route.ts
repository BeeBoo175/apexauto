import { relayInspection } from "@/lib/inspection/routes";
import { parseWorkshopJobs } from "@/lib/inspection/parse-workshop";

export function GET(request: Request) {
  const source = new URL(request.url).searchParams;
  const query = new URLSearchParams();
  for (const key of ["status", "search", "pageSize", "after"]) {
    if (source.has(key)) {
      query.set(key, source.get(key)!);
    }
  }
  return relayInspection(request, `workshop/jobs?${query}`, parseWorkshopJobs);
}
