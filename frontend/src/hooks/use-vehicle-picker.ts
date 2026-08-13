"use client";

import { useState } from "react";
import { useBoundedRead } from "./use-bounded-read";
import { apiFetch } from "@/lib/api-browser";
import type { PublicVehicle } from "@/lib/vehicles/contracts";
import { parseCatalogue } from "@/lib/vehicles/parse";
import { parsePublicDetail, type PublicVehicleDetail } from "@/lib/vehicles/public-detail";

// Finds a published car in the public catalogue and reads its detail, so staff can book it for a customer.
// Only the newest search or choice may answer.
export function useVehiclePicker() {
  const read = useBoundedRead();
  const [items, setItems] = useState<PublicVehicle[]>([]);
  const [searched, setSearched] = useState(false);
  const [message, setMessage] = useState("");
  const [detail, setDetail] = useState<PublicVehicleDetail | null>(null);

  async function search(text: string) {
    const query = text.trim();
    if (!query) {
      setItems([]);
      setSearched(false);
      setMessage("");
      return;
    }
    const attempt = read.begin(true);
    if (!attempt) {
      return;
    }
    const result = await apiFetch(
      `/api/vehicles?search=${encodeURIComponent(query)}`,
      parseCatalogue,
      { signal: attempt.signal },
    );
    if (!read.isCurrent(attempt)) {
      return;
    }
    read.end(attempt);
    setSearched(true);
    setItems(result.data?.items ?? []);
    setMessage(result.data ? "" : result.message);
  }

  async function choose(id: string) {
    const attempt = read.begin(true);
    if (!attempt) {
      return;
    }
    const result = await apiFetch(`/api/vehicles/${id}`, (value) => parsePublicDetail(value, id), {
      signal: attempt.signal,
    });
    if (!read.isCurrent(attempt)) {
      return;
    }
    read.end(attempt);
    if (result.data) {
      setDetail(result.data);
      setMessage("");
      return;
    }
    setMessage(result.message);
  }

  function clear() {
    setDetail(null);
  }

  return { busy: read.busy, items, searched, message, detail, search, choose, clear };
}
