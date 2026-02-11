import { AuthForm } from "@/components/auth/AuthForm";
import { AuthPage } from "@/components/auth/AuthPage";
import { GuestOnly } from "@/components/auth/GuestOnly";
import { safeReturnTo } from "@/lib/auth/contracts";
import { demoAccounts } from "./demo-accounts";

export default async function LoginPage({
  searchParams,
}: {
  searchParams: Promise<{ returnTo?: string }>;
}) {
  const { returnTo } = await searchParams;

  return (
    <GuestOnly>
      <AuthPage title="Sign in" introduction="Welcome back to ApexAuto.">
        <AuthForm kind="login" destination={safeReturnTo(returnTo)} demoAccounts={demoAccounts} />
      </AuthPage>
    </GuestOnly>
  );
}
