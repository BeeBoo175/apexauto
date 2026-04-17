import "server-only";
import { redirect } from "next/navigation";
import { readCurrentActor } from "@/lib/auth/server";
import type { CurrentActor } from "@/lib/auth/actor";
import { requestApiJson } from "@/lib/server/http/api";
import { parseBranch } from "@/lib/branches";
import { parsePage } from "@/lib/api";
import { paginationQuery } from "@/lib/pagination";

export async function staffActor(returnTo: string) {
  const result = await readCurrentActor().catch(() => ({ status: 502, actor: null }));
  if (result.status === 401) {
    redirect(`/login?returnTo=${encodeURIComponent(returnTo)}`);
  }
  if (result.actor?.role === "Customer") {
    redirect("/account");
  }
  return result.actor;
}

export async function staffBranches(actor: CurrentActor) {
  if (actor.role !== "GeneralManager") {
    return null;
  }

  return requestApiJson(`/api/branches?${paginationQuery()}`, (value) =>
    parsePage(value, parseBranch),
  );
}
