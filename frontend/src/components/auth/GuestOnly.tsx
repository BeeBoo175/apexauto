import "server-only";

import type { ReactNode } from "react";
import { redirect } from "next/navigation";
import { readCurrentActor } from "@/lib/auth/server";
import { actorDestination } from "@/lib/auth/actor";
import { SessionCheckUnavailable } from "./SessionCheckUnavailable";

export async function GuestOnly({ children }: { children: ReactNode }) {
  let response: Awaited<ReturnType<typeof readCurrentActor>>;

  try {
    response = await readCurrentActor();
  } catch {
    return <SessionCheckUnavailable />;
  }

  if (response.status === 200 && response.actor) {
    redirect(actorDestination(response.actor));
  }

  if (response.status !== 401) {
    return <SessionCheckUnavailable />;
  }

  return children;
}
