import { test, expect } from "@playwright/test";
import {
  acknowledgeDisclosure,
  DISCLOSURE_STORAGE_KEY,
  readDisclosureState,
  readServerDisclosureState,
} from "@/lib/demo/disclosure";

const original = Object.getOwnPropertyDescriptor(globalThis, "localStorage");

function replaceStorage(storage: unknown) {
  Object.defineProperty(globalThis, "localStorage", { configurable: true, value: storage });
}

test.afterEach(() => {
  if (original) {
    Object.defineProperty(globalThis, "localStorage", original);
  } else {
    Reflect.deleteProperty(globalThis, "localStorage");
  }
});

test("the server render never shows the dialog", () => {
  expect(readServerDisclosureState()).toBe("unknown");
});

test("acknowledgement is read from storage and written under one key", () => {
  const values = new Map<string, string>();
  replaceStorage({
    getItem: (key: string) => values.get(key) ?? null,
    setItem: (key: string, value: string) => values.set(key, value),
  });

  acknowledgeDisclosure();

  expect(values.get(DISCLOSURE_STORAGE_KEY)).toBe("true");
  expect(readDisclosureState()).toBe("acknowledged");
});
