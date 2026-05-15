using Dealership.Domain.Common;

namespace Dealership.Domain.Inspections;

public sealed record InspectionRepairItem(string PointCode, string Work);

public sealed class InspectionRepairScope
{
    public InspectionRepairScope(IReadOnlyCollection<InspectionRepairItem> items, decimal budget)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count is < 1 or > 210 || items.Any(item => item is null) ||
            items.Select(item => item.PointCode).Distinct(StringComparer.Ordinal).Count() != items.Count)
        {
            throw new ArgumentException("Repair scope requires one work description per selected finding.");
        }

        if (budget < 0 || budget > MoneyStorage.MaximumAmount || decimal.Round(budget, 2) != budget)
        {
            throw new ArgumentOutOfRangeException(nameof(budget), "Repair budget requires exact nonnegative MYR minor units.");
        }

        var normalized = items.Select(Normalize).OrderBy(item => item.PointCode, StringComparer.Ordinal).ToArray();
        Items = Array.AsReadOnly(normalized);
        Budget = budget;
    }

    public IReadOnlyList<InspectionRepairItem> Items { get; }
    public decimal Budget { get; }

    private static InspectionRepairItem Normalize(InspectionRepairItem item)
    {
        if (string.IsNullOrWhiteSpace(item.PointCode) || string.IsNullOrWhiteSpace(item.Work) ||
            item.Work.Length > 1000 || item.Work.Any(char.IsControl))
        {
            throw new ArgumentException("Repair work requires a finding code and up to 1000 non-control characters.");
        }

        return item with { Work = item.Work.Trim() };
    }
}
