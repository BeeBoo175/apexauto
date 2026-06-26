import type { Metadata } from "next";
import { AuthPage } from "@/components/auth/AuthPage";
import { StaffActivationForm } from "@/components/auth/StaffActivationForm";

export const metadata: Metadata = {
  title: "Activate account | ApexAuto",
  robots: { index: false, follow: false },
};

export default function ActivateAccountPage() {
  return (
    <AuthPage
      title="Activate your account"
      introduction="Choose a username and password to finish joining ApexAuto."
      links={<a href="/login">Return to sign in</a>}
    >
      <StaffActivationForm />
    </AuthPage>
  );
}
