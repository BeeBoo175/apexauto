import { AuthForm } from "@/components/auth/AuthForm";
import { Card } from "@/components/ui/Card";
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
    <section className="max-w-lg mx-auto p-6 flex flex-col gap-5">
      <h1 className="text-2xl font-bold">Sign in</h1>
      <Card>
        <AuthForm kind="login" destination={safeReturnTo(returnTo)} demoAccount={demoAccount} />
      </Card>
      <a href="/forgot-password" className="underline">
        Forgot password?
      </a>
      <a href="/register" className="underline">
        Create a customer account
      </a>
    </section>
  );
}
