// Branch accent colours follow the branch's position in the ordered list the calendar shows, so a new branch needs
// no code change; the stylesheet repeats its palette when there are more branches than colours.
export function branchPosition(branchId: string | undefined, orderedBranchIds: readonly string[]) {
  const position = branchId ? orderedBranchIds.indexOf(branchId) : -1;
  return position < 0 ? 0 : position;
}
