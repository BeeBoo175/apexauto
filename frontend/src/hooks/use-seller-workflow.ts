"use client";
import { useCallback, useEffect, useState } from "react";
import { useApiCommand } from "@/hooks/use-api-command";
import { parseBooking, parseQuote } from "@/lib/intake/parse";
import type { PhotoAngle, PhotoReceipt, Quote, SellerSnapshot } from "@/lib/intake/contracts";
import { emptyVehicle, parseVehicleDraft, type VehicleDraft } from "@/lib/intake/vehicle-draft";
import { bookingMatchesRequest } from "@/lib/intake/receipt";

const handoffKey = "apexauto-seller-vehicle-handoff";

export function useSellerWorkflow(initial: SellerSnapshot) {
  const [step, setStep] = useState(0);
  const [vehicle, setVehicle] = useState<VehicleDraft>(emptyVehicle);
  const [quote, setQuote] = useState<Quote | null>(null);
  const [freshQuote, setFreshQuote] = useState<Quote | null>(null);
  const [photos, setPhotos] = useState<Partial<Record<PhotoAngle, PhotoReceipt>>>({});
  const [locks, setLocks] = useState<Partial<Record<PhotoAngle, boolean>>>({});
  const valuation = useApiCommand("/api/intake/valuation", parseQuote);
  const booking = useApiCommand(
    "/api/intake/appointments",
    (value) => parseBooking(value) ?? parseQuote(value),
    (receipt, body) => bookingMatchesRequest(receipt, body),
  );
  const receipt = parseBooking(booking.receipt);
  const locked =
    valuation.busy ||
    valuation.uncertain ||
    booking.busy ||
    booking.uncertain ||
    Object.values(locks).some(Boolean);

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

  async function requestQuote() {
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
    }
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
    const result = await booking.submit({
      id: crypto.randomUUID(),
      branchId,
      startsAt,
      registrationPlate: vehicle.registrationPlate,
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
    verified: initial.account?.emailVerified === true,
  };
}
