"use client";

import { Button } from "@/components/ui/Button";
import { useToast } from "@/components/ui/Toast";
import styles from "./StaffRecordContent.module.css";

const methods = {
  phone: {
    label: "Mobile",
    copy: "Copy number",
    copied: "Phone number copied.",
  },
  email: {
    label: "Email",
    copy: "Copy email",
    copied: "Email address copied.",
  },
};

export function StaffContact({ method, value }: { method: keyof typeof methods; value: string }) {
  const toast = useToast();
  const labels = methods[method];

  async function copy() {
    try {
      await navigator.clipboard.writeText(value);
      toast.success(labels.copied);
    } catch {
      toast.error("Could not copy. Select the contact detail shown above and copy it manually.");
    }
  }

  return (
    <div className={styles.contactMethod}>
      <dt>{labels.label}</dt>
      <dd>
        <span className={styles.contactValue}>{value || "Not on file"}</span>
        <Button
          size="compact"
          variant="secondary"
          aria-label={labels.copy}
          disabled={!value}
          onClick={() => void copy()}
        >
          Copy
        </Button>
      </dd>
    </div>
  );
}
