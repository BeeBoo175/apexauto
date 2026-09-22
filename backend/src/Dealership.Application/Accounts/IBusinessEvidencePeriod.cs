namespace Dealership.Application.Accounts;

// The business-evidence years currently in force, so closure and erasure screens name the real retention period.
public interface IBusinessEvidencePeriod
{
    Task<int> YearsAsync(CancellationToken cancellationToken);
}
