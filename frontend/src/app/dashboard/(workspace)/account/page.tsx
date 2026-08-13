import { StaffAccount } from "@/components/account/StaffAccount";
import { StaffEntry } from "@/components/staff/StaffEntry";
import { LoadingProgressReady } from "@/components/ui/LoadingProgress";
import { readOwnProfile } from "@/lib/account/server";
import { workspaceMetadata } from "@/lib/staff/navigation";

export const metadata = workspaceMetadata("/dashboard/account");

export default function StaffAccountPage() {
  return (
    <StaffEntry
      path="/dashboard/account"
      access={() => true}
      allowUnverified
      deniedTitle="Account unavailable"
      deniedDescription="Your account could not be opened."
    >
      {async (actor) => {
        const response = await readOwnProfile();
        if (!response.data || response.data.userId !== actor.userId) {
          throw new Error("Account service unavailable");
        }
        return (
          <>
            <LoadingProgressReady />
            <StaffAccount key={response.data.userId} profile={response.data} />
          </>
        );
      }}
    </StaffEntry>
  );
}
