"use client";

import { useState } from "react";
import { privateApiFetch } from "@/lib/api-browser";

export interface SaveNotice {
  status: number;
  message: string;
  uncertain: boolean;
}

// One administrative write at a time; a lost response is reported as unconfirmed and never retried silently.
export function useSettingsSave<T>(parse: (value: unknown) => T | null) {
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<SaveNotice | null>(null);

  async function save(path: string, method: "POST" | "PUT", body: unknown) {
    if (busy) {
      return null;
    }
    setBusy(true);
    setNotice(null);
    try {
      const result = await privateApiFetch(path, parse, {
        method,
        headers: { "content-type": "application/json" },
        body: JSON.stringify(body),
      });
      if (result.status === 200 && result.data) {
        return result.data;
      }
      setNotice({ status: result.status, message: result.message, uncertain: result.uncertain });
      return null;
    } catch {
      setNotice({
        status: 502,
        uncertain: true,
        message:
          "The result is unconfirmed. Your entries are kept; reload to check before retrying.",
      });
      return null;
    } finally {
      setBusy(false);
    }
  }

  return { busy, notice, save, clearNotice: () => setNotice(null) };
}
