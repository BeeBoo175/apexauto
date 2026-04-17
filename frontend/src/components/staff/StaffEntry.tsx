import "server-only";
import type { ReactNode } from "react";
import { SessionCheckUnavailable } from "@/components/auth/SessionCheckUnavailable";
import type { CurrentActor } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";
import { staffActor } from "@/lib/staff/server";
import { StaffActorBoundary } from "./StaffActorBoundary";
import { StaffLink } from "./StaffLink";
import { StaffNotice } from "./StaffPage";
import { LoadingProgressReady } from "@/components/ui/LoadingProgress";

export async function StaffEntry({
  path,
  access,
  deniedTitle,
  deniedDescription,
  searchParams,
  children,
}: {
  path: string;
  access: (actor: CurrentActor) => boolean;
  deniedTitle: string;
  deniedDescription: string;
  searchParams?: Promise<SearchParams>;
  children: (actor: CurrentActor) => Promise<ReactNode>;
}) {
  const actor = await staffActor(path);
  if (!actor) {
    return (
      <>
        <LoadingProgressReady />
        <SessionCheckUnavailable />
      </>
    );
  }

  // Run page reads only after the current request's actor and feature access are confirmed.
  const permitted = access(actor);
  const parameters = permitted && searchParams ? await searchParams : {};
  const content = permitted ? (
    await children(actor)
  ) : (
    <>
      <LoadingProgressReady />
      <StaffNotice title={deniedTitle}>
        <p>{deniedDescription}</p>
        <StaffLink href="/dashboard">Return to your staff workspace</StaffLink>
      </StaffNotice>
    </>
  );

  return (
    <StaffActorBoundary
      actor={actor}
      path={path}
      parameters={parameters}
      snapshotId={crypto.randomUUID()}
    >
      {content}
    </StaffActorBoundary>
  );
}
