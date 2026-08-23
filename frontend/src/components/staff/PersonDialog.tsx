"use client";

import { Avatar } from "@/components/ui/Avatar";
import { Dialog } from "@/components/ui/Dialog";
import { usePersonPreview } from "@/hooks/use-person-preview";
import type { Person } from "@/lib/profile/contracts";
import { roleNames } from "@/lib/auth/actor";
import styles from "./PersonDialog.module.css";

const EMPTY = "—";

// The one place a person is shown in place: opened from any label, queue row or drawer, over the current work.
// The layout never depends on the caller or on what the viewer may see; details they cannot see read as a dash,
// and the loading shell holds the same slots, so nothing moves when the profile arrives.
export function PersonDialog({
  id,
  name,
  open,
  onClose,
}: {
  id: string;
  name: string;
  open: boolean;
  onClose: () => void;
}) {
  const { person, settled } = usePersonPreview(id, open, 0);
  const customer = person?.role === "Customer";

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={person?.name ?? name}
      description={person ? summary(person) : undefined}
    >
      {settled && !person ? (
        <p className={styles.failed}>
          This profile could not be loaded. Reload the page to try again.
        </p>
      ) : (
        <div className={styles.body} aria-busy={!person}>
          <PersonPicture id={id} name={name} person={person} />
          <dl className={styles.facts}>
            <Fact
              label="Username"
              value={person ? `@${person.username}` : null}
              loading={!person}
            />
            <Fact
              label={customer ? "Account" : "Branch"}
              value={person ? accountSlot(person) : null}
              loading={!person}
            />
            {!customer && (
              <>
                <Fact label="Work mobile" value={person?.mobile} loading={!person} />
                <Fact label="Email" value={person?.email} loading={!person} />
              </>
            )}
          </dl>
        </div>
      )}
    </Dialog>
  );
}

function PersonPicture({ id, name, person }: { id: string; name: string; person: Person | null }) {
  return (
    <Avatar
      accountId={id}
      name={person?.name ?? name}
      pictureId={person?.pictureId ?? null}
      size="lg"
    />
  );
}

function Fact({
  label,
  value,
  loading,
}: {
  label: string;
  value: string | null | undefined;
  loading: boolean;
}) {
  return (
    <div>
      <dt>{label}</dt>
      <dd data-pending={loading}>{loading ? "" : (value ?? EMPTY)}</dd>
    </div>
  );
}

function summary(person: Person) {
  const inactive = person.role === "Customer" ? "Suspended" : "Deactivated";
  const parts = [roleNames[person.role], person.isActive ? null : inactive];
  return parts.filter(Boolean).join(" · ");
}

// A customer has no branch, so that slot tells staff whether they can sign in.
function accountSlot(person: Person) {
  if (person.role !== "Customer") {
    return person.branchName;
  }
  return person.isActive ? "Active" : "Suspended";
}
