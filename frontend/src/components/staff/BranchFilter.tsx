import { Button } from "@/components/ui/Button";
import { Select } from "@/components/ui/Input";
import type { Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";

interface Props {
  branches: Page<Branch> | null;
  branchId: string;
  selectedBranchName?: string;
  disabled: boolean;
  onChange: (branchId: string) => void;
  onPage: (page: number) => void;
  id?: string;
  labelHidden?: boolean;
  showTitle?: boolean;
  paginationClassName?: string;
}

export function BranchFilter({
  branches,
  branchId,
  selectedBranchName,
  disabled,
  onChange,
  onPage,
  id,
  labelHidden = true,
  showTitle = false,
  paginationClassName,
}: Props) {
  const selectedBranch = branches?.items.find((branch) => branch.id === branchId);
  const branchName = selectedBranch?.name ?? selectedBranchName ?? "Selected branch";
  const title = branchId ? branchName : "All branches";
  const pagination = branches && branches.total > branches.pageSize && (
    <>
      <Button
        size="compact"
        variant="secondary"
        type="button"
        disabled={disabled || branches.page === 1}
        onClick={() => onPage(branches.page - 1)}
      >
        Previous branches
      </Button>
      <Button
        size="compact"
        variant="secondary"
        type="button"
        disabled={disabled || branches.page * branches.pageSize >= branches.total}
        onClick={() => onPage(branches.page + 1)}
      >
        More branches
      </Button>
    </>
  );

  return (
    <>
      <Select
        label="Branch"
        labelHidden={labelHidden}
        density="compact"
        id={id}
        value={branchId}
        title={showTitle ? title : undefined}
        disabled={disabled}
        onChange={(event) => onChange(event.target.value)}
      >
        <option value="">All branches</option>
        {branchId && !selectedBranch && <option value={branchId}>{branchName}</option>}
        {branches?.items.map((branch) => (
          <option key={branch.id} value={branch.id}>
            {branch.name}
          </option>
        ))}
      </Select>
      {!branches && (
        <Button
          size="compact"
          variant="secondary"
          type="button"
          disabled={disabled}
          onClick={() => onPage(1)}
        >
          Retry branches
        </Button>
      )}
      {pagination &&
        (paginationClassName ? (
          <div className={paginationClassName}>{pagination}</div>
        ) : (
          pagination
        ))}
    </>
  );
}
