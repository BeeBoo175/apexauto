import "server-only";
import type { ReactNode } from "react";
import { SessionCheckUnavailable } from "@/components/auth/SessionCheckUnavailable";
import type { CurrentActor } from "@/lib/auth/actor";
import { staffActor } from "@/lib/staff/server";
import { StaffActorBoundary } from "./StaffActorBoundary";
import { StaffLink } from "./StaffLink";
import { StaffNotice } from "./StaffPage";

export async function StaffEntry({
  path,
  access,
  deniedTitle,
  deniedDescription,
  children,
}: {
  path: string;
  access: (actor: CurrentActor) => boolean;
  deniedTitle: string;
  deniedDescription: string;
  children: (actor: CurrentActor) => Promise<ReactNode>;
}) {
  const actor = await staffActor(path);
  if (!actor) {
    return <SessionCheckUnavailable />;
  }

  // Run page reads only after the current request's actor and feature access are confirmed.
  const content = access(actor) ? (
    await children(actor)
  ) : (
    <StaffNotice title={deniedTitle}>
      <p>{deniedDescription}</p>
      <StaffLink href="/dashboard">Return to your staff workspace</StaffLink>
    </StaffNotice>
  );

  return (
    <StaffActorBoundary actor={actor} path={path} snapshotId={crypto.randomUUID()}>
      {content}
    </StaffActorBoundary>
  );
}
