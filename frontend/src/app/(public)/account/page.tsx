import { redirect } from "next/navigation";
import { AccountPanel } from "@/components/auth/AccountPanel";
import { Card } from "@/components/ui/Card";
import { readOwnAccount } from "@/lib/auth/server";

export default async function AccountPage() {
  const response = await readOwnAccount();

  if (response.status === 401 || response.status === 403) {
    redirect("/login?returnTo=%2Faccount");
  }

  if (!response.account) {
    throw new Error("Account service unavailable");
  }

  return (
    <section className="max-w-lg mx-auto p-6 flex flex-col gap-5">
      <h1 className="text-2xl font-bold">Your account</h1>
      <Card>
        <AccountPanel key={response.account.userId} initialAccount={response.account} />
      </Card>
    </section>
  );
}
