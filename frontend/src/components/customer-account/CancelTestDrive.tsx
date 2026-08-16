"use client";

import { ConfirmationDialog } from "@/components/ui/ConfirmationDialog";
import { useApiCommand } from "@/hooks/use-api-command";
import { privateApiFetch } from "@/lib/api-browser";
import { isRecord } from "@/lib/auth/contracts";
import { formatMalaysianDateTime } from "@/lib/date-format";
import type { TestDriveHistoryItem } from "@/lib/test-drives/account";
import { parseTestDriveCancellation } from "@/lib/test-drives/customer";

export function CancelTestDrive({
  item,
  actorId,
  onClose,
  onConfirmed,
  onChanged,
}: {
  item: TestDriveHistoryItem;
  actorId: string;
  onClose: () => void;
  onConfirmed: (receipt: NonNullable<ReturnType<typeof parseTestDriveCancellation>>) => void;
  onChanged: () => void;
}) {
  const command = useApiCommand(
    `/api/account/test-drives/${item.id}/cancellations`,
    parseTestDriveCancellation,
    (receipt, body) =>
      isRecord(body) &&
      receipt.id === body.operationId &&
      receipt.bookingId === item.id &&
      receipt.customerId === actorId &&
      receipt.kind === "CustomerCancelled",
    privateApiFetch,
  );

  async function cancel() {
    const result = await command.submit(
      command.uncertain ? null : { operationId: crypto.randomUUID() },
    );
    if (result?.status === 200 && result.data) {
      onConfirmed(result.data);
      onClose();
      return;
    }
    if (result?.status === 409 || result?.status === 404) {
      onClose();
      onChanged();
    }
  }

  return (
    <ConfirmationDialog
      open
      title="Cancel this test drive?"
      danger
      confirmLabel={command.uncertain ? "Reconcile cancellation" : "Confirm cancellation"}
      cancelLabel="Keep test drive"
      busy={command.busy}
      locked={command.uncertain}
      onClose={onClose}
      onConfirm={cancel}
      facts={[
        {
          label: "Vehicle",
          value: `${item.manufactureYear} ${item.make} ${item.model} ${item.trim}`,
        },
        { label: "Branch", value: item.branchName },
        { label: "Time (Malaysia)", value: formatMalaysianDateTime(item.startsAt) },
      ]}
    >
      <p>
        Cancellation is available before the booked start, provided the drive has not started. Your
        booking remains in history.
      </p>
    </ConfirmationDialog>
  );
}
