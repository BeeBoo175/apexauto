import type { WorkspaceTab } from "@/hooks/use-workspace-tabs";
import { StaffQueueTabs } from "./StaffQueueTabs";

export function StaffWorkspaceTabs<T extends string>({
  label,
  tabs,
  value,
  disabled = false,
  onChange,
}: {
  label: string;
  tabs: readonly WorkspaceTab<T>[];
  value: T;
  disabled?: boolean;
  onChange: (section: T) => void;
}) {
  return (
    <StaffQueueTabs
      label={label}
      tabs={tabs}
      value={value}
      disabled={disabled}
      onChange={onChange}
    />
  );
}
