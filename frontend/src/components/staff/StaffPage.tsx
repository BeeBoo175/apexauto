"use client";

import { useContext, type ReactNode, type Ref } from "react";
import { Card } from "@/components/ui/Card";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { StaffNavigationContext } from "./StaffActorBoundary";
import { StaffEntryPresentationContext } from "./StaffClientEntryBoundary";
import { StaffLoadingShell, type StaffLoadingView } from "./StaffLoadingShell";
import reportStyles from "@/components/inspection/SignedReport.module.css";
import styles from "./Staff.module.css";
import loading from "./StaffLoadingShell.module.css";

const titles: Record<string, string> = {
  "/dashboard/intake": "Intake appointments",
  "/dashboard/inspection": "Workshop",
  "/dashboard/inspection/repairs": "Repair requests",
  "/dashboard/management": "Listings",
  "/dashboard/reservations": "Reservations",
  "/dashboard/settings": "Settings",
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
    "/dashboard/intake": parameters.view === "list" ? "intake" : "calendar",
    "/dashboard/inspection": workshopView,
    "/dashboard/inspection/repairs": "repairs",
    "/dashboard/reservations": "inspection",
    "/dashboard/settings": "inspection",
  };
  let view = views[pathname] ?? "inspection";
  let title = titles[pathname] ?? "Your staff workspace";
  if (pathname.startsWith("/dashboard/intake/")) {
    view = "appointment";
    title = "Intake appointment";
  }
  if (pathname.includes("/reports/")) {
    view = "report";
    title = "Signed report";
  }
  return (
    <>
      <LoadingProgress label="Opening workspace" />
      <div className={view === "report" ? reportStyles.controls : undefined}>
        <StaffCommandBar title={title} />
      </div>
      <StaffLoadingShell view={view} selected={Boolean(parameters.inspection)} />
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
