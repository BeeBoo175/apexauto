"use client";
import { useCallback, useEffect, useState } from "react";
import { useApiCommand } from "@/hooks/use-api-command";
import { parseBooking, parseQuote } from "@/lib/intake/parse";
import type { PhotoAngle, PhotoReceipt, Quote, SellerSnapshot } from "@/lib/intake/contracts";
import { normalizeMalaysianPlate, validateMalaysianPlate } from "@/lib/intake/registration-plate";
import { emptyVehicle, parseVehicleDraft, type VehicleDraft } from "@/lib/intake/vehicle-draft";
import { bookingMatchesRequest } from "@/lib/intake/receipt";
import { apiFetchFor } from "@/lib/staff/assisted-fetch";

const handoffKey = "apexauto-seller-vehicle-handoff";

// actingFor names the customer when sales staff book the trade-in on their behalf.
export function useSellerWorkflow(initial: SellerSnapshot, actingFor?: string) {
  const [step, setStep] = useState(0);
  const [vehicle, setVehicle] = useState<VehicleDraft>(emptyVehicle);
  const [plateTouched, setPlateTouched] = useState(false);
  const [plateSubmitted, setPlateSubmitted] = useState(false);
  const [quote, setQuote] = useState<Quote | null>(null);
  const [freshQuote, setFreshQuote] = useState<Quote | null>(null);
  const [photos, setPhotos] = useState<Partial<Record<PhotoAngle, PhotoReceipt>>>({});
  const [locks, setLocks] = useState<Partial<Record<PhotoAngle, boolean>>>({});
  const valuation = useApiCommand("/api/intake/valuations", parseQuote);
  const booking = useApiCommand(
    "/api/intake/appointments",
    (value) => parseBooking(value) ?? parseQuote(value),
    (receipt, body) => bookingMatchesRequest(receipt, body),
    apiFetchFor(actingFor),
  );
  const receipt = parseBooking(booking.receipt);
  const locked =
    valuation.busy ||
    valuation.uncertain ||
    booking.busy ||
    booking.uncertain ||
    Object.values(locks).some(Boolean);
  let plateError: string | null = null;
  if (plateSubmitted) {
    plateError = validateMalaysianPlate(vehicle.registrationPlate, true);
  } else if (plateTouched) {
    plateError = validateMalaysianPlate(vehicle.registrationPlate, false);
  }

  useEffect(() => {
    try {
      const stored = sessionStorage.getItem(handoffKey);
      sessionStorage.removeItem(handoffKey);
      if (!stored) {
        return;
      }
      const value = parseVehicleDraft(JSON.parse(stored));
      if (value) {
        queueMicrotask(() => setVehicle(value));
      }
    } catch {
      /* Storage can be unavailable; the editable form remains usable. */
    }
  }, []);

  const receivePhoto = useCallback((angle: PhotoAngle, photo: PhotoReceipt | null) => {
    setPhotos((current) => ({ ...current, [angle]: photo ?? undefined }));
  }, []);
  const lockPhoto = useCallback((angle: PhotoAngle, pending: boolean) => {
    setLocks((current) => ({ ...current, [angle]: pending }));
  }, []);

  function commitPlate(required = false) {
    setPlateTouched(true);
    if (required) {
      setPlateSubmitted(true);
    }
    const normalized = normalizeMalaysianPlate(vehicle.registrationPlate);
    if (normalized !== vehicle.registrationPlate) {
      setVehicle((current) => ({ ...current, registrationPlate: normalized }));
    }
    return normalized;
  }

  async function requestQuote() {
    const normalizedPlate = commitPlate(true);
    if (validateMalaysianPlate(normalizedPlate, true)) {
      return false;
    }
    const result = await valuation.submit({
      make: vehicle.make,
      model: vehicle.model,
      trim: vehicle.trim,
      manufactureYear: Number(vehicle.manufactureYear),
      mileageKilometres: Number(vehicle.mileageKilometres),
    });
    if (result?.status === 200 && result.data) {
      setQuote(result.data);
      setFreshQuote(null);
      setStep(1);
      return true;
    }
    return false;
  }

  function saveHandoff() {
    if (initial.account || initial.access !== 401) {
      return;
    }
    try {
      sessionStorage.setItem(handoffKey, JSON.stringify(vehicle));
    } catch {
      /* Continue without persistent vehicle handoff. */
    }
  }

  async function book(branchId: string, startsAt: string) {
    if (!quote) {
      return;
    }
    const normalizedPlate = commitPlate(true);
    if (validateMalaysianPlate(normalizedPlate, true)) {
      return;
    }
    const result = await booking.submit({
      id: crypto.randomUUID(),
      branchId,
      startsAt,
      registrationPlate: normalizedPlate,
      vehicle: {
        make: vehicle.make,
        model: vehicle.model,
        trim: vehicle.trim,
        manufactureYear: Number(vehicle.manufactureYear),
        mileageKilometres: Number(vehicle.mileageKilometres),
      },
      acceptedQuote: quote,
      photoIds: Object.values(photos)
        .filter((photo): photo is PhotoReceipt => Boolean(photo))
        .map((photo) => photo.id),
    });
    if (result?.status === 409) {
      setFreshQuote(parseQuote(result.data));
    }
  }

  function editDetails(value: VehicleDraft) {
    if (locked || receipt) {
      return;
    }
    setVehicle(value);
    setQuote(null);
    setFreshQuote(null);
  }

  return {
    step,
    setStep,
    vehicle,
    plateError,
    commitPlate,
    editDetails,
    quote,
    freshQuote,
    acceptFreshQuote: () => {
      setQuote(freshQuote);
      setFreshQuote(null);
    },
    photos,
    receivePhoto,
    lockPhoto,
    locked,
    valuation,
    booking,
    receipt,
    requestQuote,
    saveHandoff,
    book,
    verified: initial.account?.emailVerified === true || actingFor !== undefined,
  };
}
