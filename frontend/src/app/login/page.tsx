import { AuthForm } from "@/components/auth/AuthForm";
import { AuthPage } from "@/components/auth/AuthPage";
import { safeReturnTo } from "@/lib/auth/contracts";

const demoAccount = {
  email: "nur.aisyah@example.invalid",
  password: "Password123!",
};

export default async function LoginPage({
  searchParams,
}: {
  searchParams: Promise<{ returnTo?: string }>;
}) {
  const { returnTo } = await searchParams;

  return (
    <AuthPage
      title="Sign in"
      introduction="Enter the email and password for your account."
      photoHeading="Sign in to ApexAuto"
      photoDescription="Access your account and manage your vehicle intake."
      links={
        <>
          <a href="/forgot-password">Forgot password?</a>
          <a href="/register">Create a customer account</a>
        </>
      }
    >
      <AuthForm kind="login" destination={safeReturnTo(returnTo)} demoAccount={demoAccount} />
    </AuthPage>
  );
}
