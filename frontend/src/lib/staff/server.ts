import "server-only";
import { redirect } from "next/navigation";
import { readCurrentActor } from "@/lib/auth/server";

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
