using Dealership.Application.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

// Demo pictures go through the canonical upload, so they are validated, thumbnailed and stored like a real one.
public sealed class DevelopmentAccountPictureFixtures(DealershipDbContext database, AccountPictureService pictures)
{
    public async Task<IReadOnlyList<DevelopmentFixtureReport>> RunAsync(bool seed, CancellationToken cancellationToken)
    {
        var reports = new List<DevelopmentFixtureReport>();
        foreach (var picture in DevelopmentFixtureManifest.Pictures)
        {
            reports.Add(await RunOneAsync(picture, seed, cancellationToken));
        }

        return reports;
    }

    private async Task<DevelopmentFixtureReport> RunOneAsync(DevelopmentAccountPicture picture, bool seed,
        CancellationToken cancellationToken)
    {
        var fixture = $"Picture ({picture.Email})";
        var normalized = picture.Email.ToUpperInvariant();
        var accountId = await database.Users.AsNoTracking().Where(user => user.NormalizedEmail == normalized)
            .Select(user => (Guid?)user.Id).SingleOrDefaultAsync(cancellationToken);

        if (accountId is not { } id)
        {
            return new(fixture, "missing account", true);
        }

        // An existing current picture is preserved, including one the account owner chose.
        if (await database.AccountPictures.AnyAsync(item => item.UserId == id && item.RemovedAt == null, cancellationToken))
        {
            return new(fixture, "matches", false);
        }

        if (!seed)
        {
            return new(fixture, "missing", true);
        }

        await using var content = Open(picture.File);
        var result = await pictures.UploadAsync(id, "image/jpeg", content, cancellationToken);
        return result.Status == AccountPictureStatus.Stored
            ? new(fixture, "created", false)
            : new(fixture, $"upload {result.Status}", true);
    }

    private static Stream Open(string file)
    {
        var name = $"Dealership.Infrastructure.Development.Media.{file}";
        return typeof(DevelopmentAccountPictureFixtures).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("The development account picture resource is missing.");
    }
}
