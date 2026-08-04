import { isUuid } from "@/lib/auth/contracts";

// Segments after the section belong to the feature (Workshop keeps a retained repair request there), so they
// are returned in `trail` for the feature parser to accept or reject.
export function staffRecordLocation(pathname: string, workspace: string) {
  if (pathname === workspace) {
    return { id: "", section: "", trail: [] as string[] };
  }
  if (!pathname.startsWith(`${workspace}/`)) {
    return null;
  }
  const [id, section = "", ...trail] = pathname.slice(workspace.length + 1).split("/");
  if (!isUuid(id)) {
    return null;
  }
  return { id, section, trail };
}
