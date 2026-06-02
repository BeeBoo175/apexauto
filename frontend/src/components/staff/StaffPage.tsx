"use client";

import { useContext, type ReactNode, type Ref } from "react";
import { Card } from "@/components/ui/Card";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { StaffNavigationContext } from "./StaffActorBoundary";
import { StaffEntryPresentationContext } from "./StaffClientEntryBoundary";
import { StaffLoadingShell, type StaffLoadingView } from "./StaffLoadingShell";
import styles from "./Staff.module.css";
import loading from "./StaffLoadingShell.module.css";
import { intakeView } from "@/lib/staff/view-memory";

const titles: Record<string, string> = {
  "/dashboard/intake": "Intake appointments",
  "/dashboard/inspection": "Workshop",
  "/dashboard/inspection/repairs": "Repair requests",
  "/dashboard/management": "Listings",
  "/dashboard/settings": "Settings",
  "/dashboard/sales": "Sales",
  "/dashboard/test-drives": "Test drives",
};

export function StaffLoading() {
  const presentation = useContext(StaffEntryPresentationContext);

  if (!presentation) {
    throw new Error("Staff loading requires its authorized page presentation");
  }

  const { actor, path: pathname, parameters } = presentation;
  const workshopView: StaffLoadingView =
    actor.role === "WorkshopTechnician" ? "technician" : "inspection";
  const views: Record<string, StaffLoadingView> = {
    "/dashboard/intake": "intake",
    "/dashboard/inspection": workshopView,
    "/dashboard/inspection/repairs": "repairs",
    "/dashboard/settings": "inspection",
    // Agents get the queue workspace and managers the Sales table, so the shell follows the role.
    "/dashboard/sales": actor.role === "SalesAgent" ? "technician" : "inspection",
    "/dashboard/test-drives": actor.role === "SalesAgent" ? "technician" : "inspection",
  };
  let view = views[pathname] ?? "inspection";
  let title = titles[pathname] ?? "Your staff workspace";
  if (pathname === "/dashboard/intake") {
    const requested = intakeView(parameters.view, actor.role === "SalesAgent");
    const intakeShells: Record<typeof requested, StaffLoadingView> = {
      calendar: "calendar",
      list: "intake",
      queue: "technician",
    };
    view = intakeShells[requested];
  }
  if (pathname.startsWith("/dashboard/intake/")) {
    view = "appointment";
    title = "Intake appointment";
  }
  return (
    <>
      <LoadingProgress label="Opening workspace" />
      <StaffCommandBar title={title} />
      <StaffLoadingShell
        view={view}
        selected={Boolean(
          parameters.inspection || parameters.sale || parameters.appointment || parameters.booking,
        )}
      />
    </>
  );
}

export function StaffCommandBar({
  title,
  actions,
  actionsRef,
}: {
  title: string | null;
  actions?: ReactNode;
  actionsRef?: Ref<HTMLDivElement>;
}) {
  const navigation = useContext(StaffNavigationContext);
  const pendingTitle = navigation?.pendingPath ? titles[navigation.pendingPath] : undefined;
  return (
    <header className={styles.commandBar}>
      {title === null ? (
        <span className={loading.commandTitle} aria-hidden="true" />
      ) : (
        <h1 tabIndex={-1}>{pendingTitle ?? title}</h1>
      )}
      {(actions || actionsRef) && (
        <div ref={actionsRef} className={styles.actions}>
          {actions}
        </div>
      )}
    </header>
  );
}

export function StaffNotice({ title, children }: { title: string; children: ReactNode }) {
  return (
    <>
      <StaffCommandBar title={title} />
      <Card className={styles.notice}>{children}</Card>
    </>
  );
}
