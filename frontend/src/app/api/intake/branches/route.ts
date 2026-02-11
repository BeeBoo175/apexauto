import { relayIntake } from "@/lib/intake/routes";
import { parseBranch, parsePage } from "@/lib/intake/parse";
export function GET(request: Request) {
  const page = new URL(request.url).searchParams.get("page") ?? "1";
  return relayIntake(
    request,
    `/api/branches?page=${encodeURIComponent(page)}&pageSize=20`,
    (value) => parsePage(value, parseBranch),
  );
}
