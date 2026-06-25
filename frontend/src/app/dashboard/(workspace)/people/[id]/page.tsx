import { notFound } from "next/navigation";
import { PersonClientEntry } from "@/components/account/PersonClientEntry";
import { StaffEntry } from "@/components/staff/StaffEntry";
import { isUuid } from "@/lib/auth/contracts";
import { hasPersonAccess } from "@/lib/account/contracts";

export default async function PersonPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  if (!isUuid(id)) {
    notFound();
  }
  return (
    <StaffEntry
      path={`/dashboard/people/${id}`}
      access={hasPersonAccess}
      deniedTitle="Profile access unavailable"
      deniedDescription="Only staff can view other people's profiles."
    >
      {async () => <PersonClientEntry key={crypto.randomUUID()} id={id} />}
    </StaffEntry>
  );
}
