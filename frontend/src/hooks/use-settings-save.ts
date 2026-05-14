"use client";

import { useRef, useState } from "react";
import { privateApiFetch } from "@/lib/api-browser";
import { isRecord, isUuid } from "@/lib/auth/contracts";

export interface SaveNotice {
  status: number;
  message: string;
  uncertain: boolean;
}

// One administrative write at a time; a lost response is reported as unconfirmed and never retried silently.
export function useSettingsSave<T>(parse: (value: unknown) => T | null) {
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<SaveNotice | null>(null);
  const captured = useRef<{
    path: string;
    method: "POST" | "PUT";
    body: string;
    readPath: string;
    createConflict: boolean;
  } | null>(null);
  const inFlight = useRef(false);

  async function save(path: string, method: "POST" | "PUT", body: unknown) {
    if (inFlight.current) {
      return null;
    }
    inFlight.current = true;
    setBusy(true);
    captured.current ??= {
      path,
      method,
      body: JSON.stringify(body),
      readPath:
        method === "POST" && isRecord(body) && isUuid(body.id) ? `${path}/${body.id}` : path,
      createConflict: false,
    };
    const request = captured.current;
    const reconciling = Boolean(notice?.uncertain);
    setNotice(null);
    try {
      const result = await privateApiFetch(request.path, parse, {
        method: request.method,
        headers: { "content-type": "application/json" },
        body: request.body,
      });
      if (result.status === 200 && result.data) {
        captured.current = null;
        return result.data;
      }
      const uncertain = reconciling || result.uncertain || result.status >= 500;
      request.createConflict =
        request.method === "POST" && result.status === 409 && !result.uncertain;
      if (!uncertain) {
        captured.current = null;
      }
      setNotice({ status: result.status, message: result.message, uncertain });
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
      inFlight.current = false;
      setBusy(false);
    }
  }

  async function reload(
    path: string,
  ): Promise<{ status: "loaded"; data: T } | { status: "missing" | "unavailable" }> {
    if (inFlight.current) {
      return { status: "unavailable" };
    }
    inFlight.current = true;
    setBusy(true);
    try {
      const result = await privateApiFetch(path, parse);
      if (result.status === 200 && result.data) {
        captured.current = null;
        setNotice(null);
        return { status: "loaded", data: result.data };
      }
      // A serialized create replay's definitive conflict followed by absence rules out that captured ID.
      if (
        result.status === 404 &&
        captured.current?.createConflict &&
        captured.current.readPath === path
      ) {
        captured.current = null;
        setNotice({
          status: 404,
          uncertain: false,
          message: "No saved record was found for this create. Your entries are kept.",
        });
        return { status: "missing" };
      }
      return { status: "unavailable" };
    } catch {
      return { status: "unavailable" };
    } finally {
      inFlight.current = false;
      setBusy(false);
    }
  }

  return {
    busy,
    notice,
    save,
    reload,
    clearNotice: () => {
      if (!notice?.uncertain) {
        setNotice(null);
      }
    },
  };
}
