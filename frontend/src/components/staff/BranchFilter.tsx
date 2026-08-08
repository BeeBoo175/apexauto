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
  label?: string;
  labelHidden?: boolean;
  showTitle?: boolean;
  paginationClassName?: string;
  density?: "default" | "compact";
  emptyLabel?: string;
}

export function BranchFilter({
  branches,
  branchId,
  selectedBranchName,
  disabled,
  onChange,
  onPage,
  id,
  label = "Branch",
  labelHidden = true,
  showTitle = false,
  paginationClassName,
  density = "compact",
  emptyLabel = "All branches",
}: Props) {
  const selectedBranch = branches?.items.find((branch) => branch.id === branchId);
  const branchName = selectedBranch?.name ?? selectedBranchName ?? "Selected branch";
  const title = branchId ? branchName : emptyLabel;
  const buttonSize = density === "compact" ? "compact" : "md";
  const pagination = branches && branches.total > branches.pageSize && (
    <>
      <Button
        size={buttonSize}
        variant="secondary"
        type="button"
        disabled={disabled || branches.page === 1}
        onClick={() => onPage(branches.page - 1)}
      >
        Previous branches
      </Button>
      <Button
        size={buttonSize}
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
        label={label}
        labelHidden={labelHidden}
        density={density}
        id={id}
        value={branchId}
        title={showTitle ? title : undefined}
        disabled={disabled}
        onChange={(event) => onChange(event.target.value)}
      >
        <option value="">{emptyLabel}</option>
        {branchId && !selectedBranch && <option value={branchId}>{branchName}</option>}
        {branches?.items.map((branch) => (
          <option key={branch.id} value={branch.id}>
            {branch.name}
          </option>
        ))}
      </Select>
      {!branches && (
        <Button
          size={buttonSize}
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
