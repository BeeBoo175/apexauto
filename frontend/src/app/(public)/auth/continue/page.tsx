import { redirect } from "next/navigation";
import { readCurrentActor } from "@/lib/auth/server";
import { actorDestination } from "@/lib/auth/actor";
import { authHref, safeReturnTo } from "@/lib/auth/contracts";
import { SessionCheckUnavailable } from "@/components/auth/SessionCheckUnavailable";

export const metadata = { robots: { index: false, follow: false } };

export default async function ContinuePage({
  searchParams,
}: {
  searchParams: Promise<{ returnTo?: string }>;
}) {
  const returnTo = safeReturnTo((await searchParams).returnTo);
  const result = await readCurrentActor().catch(() => ({ status: 502, actor: null }));
  if (result.status === 401) {
    redirect(authHref("/login", returnTo));
  }
  if (!result.actor) {
    return <SessionCheckUnavailable />;
  }
  redirect(actorDestination(result.actor, returnTo));
}
