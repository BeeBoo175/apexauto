"use client";

import type { ReactNode } from "react";
import { useGuardedAction } from "@/hooks/use-guarded-action";
import styles from "./StaffWorkspace.module.css";

export function StaffWorkspaceFooter({
  title,
  description,
  children,
}: {
  title: string;
  description: string;
  children?: ReactNode;
}) {
  const interaction = useGuardedAction();
  return (
    <footer className={styles.footer}>
      <div className={styles.footerContext}>
        <strong>{title}</strong>
        <span>{interaction.message ?? description}</span>
      </div>
      <div className={styles.footerActions}>{children}</div>
    </footer>
  );
}
