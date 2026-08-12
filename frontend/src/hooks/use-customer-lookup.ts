"use client";

import { useState } from "react";
import { useBoundedRead } from "./use-bounded-read";
import { privateApiFetch } from "@/lib/api-browser";
import {
  CUSTOMER_LOOKUP_MINIMUM,
  parseCustomerLookup,
  type CustomerLookupRow,
} from "@/lib/staff/assisted-customer";

// Searches the customers a staff member may act for. Only the newest search may answer.
export function useCustomerLookup() {
  const read = useBoundedRead();
  const [items, setItems] = useState<CustomerLookupRow[]>([]);
  const [message, setMessage] = useState("");
  const [searched, setSearched] = useState(false);

  async function search(text: string) {
    const query = text.trim();
    if (query.length < CUSTOMER_LOOKUP_MINIMUM) {
      setItems([]);
      setSearched(false);
      setMessage("");
      return;
    }
    const attempt = read.begin(true);
    if (!attempt) {
      return;
    }
    const result = await privateApiFetch(
      `/api/staff/customer-lookup?search=${encodeURIComponent(query)}`,
      parseCustomerLookup,
      { signal: attempt.signal },
    );
    if (!read.isCurrent(attempt)) {
      return;
    }
    read.end(attempt);
    setSearched(true);
    if (result.data) {
      setItems(result.data.items);
      setMessage("");
      return;
    }
    setItems([]);
    setMessage(result.message);
  }

  return { busy: read.busy, items, message, searched, search };
}
