import type { Metadata } from "next";
import { AuthPage } from "@/components/auth/AuthPage";
import { ConfirmDepositControl } from "@/components/auth/ConfirmDepositControl";

export const metadata: Metadata = {
  title: "Confirm deposit | ApexAuto",
  robots: { index: false, follow: false },
};

export default function ConfirmDepositPage() {
  return (
    <AuthPage
      title="Confirm your deposit"
      introduction="Our team asked to reserve a car for you. Confirm the deposit to complete the reservation."
    >
      <ConfirmDepositControl />
    </AuthPage>
  );
}
