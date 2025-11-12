using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Dealership.Application.Accounts;
using Dealership.Infrastructure.Configuration;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace Dealership.Infrastructure.Identity;

public sealed class AccountSessionStore(DealershipDbContext database, UserManager<AccountUser> users,
    IOptions<JwtSettings> settings, TimeProvider clock, MissingAccountPasswordHash missingAccountHash,
    IPasswordHasher<AccountUser> passwordHasher) : IAccountSessionStore
{
    public async Task<LoginSession?> LoginAsync(Guid loginKey, string email, string password, CancellationToken cancellationToken)
    {
        var normalized = users.NormalizeEmail(email);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // Credential changes must lock this same user row before changing the password/stamp.
        var user = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM \"AspNetUsers\" WHERE \"NormalizedEmail\" = {normalized} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            // Verifies against a dummy hash to equalize response latency and mitigate timing-based user enumeration.
            missingAccountHash.Verify(passwordHasher, password);
            return null;
        }

        if (!await users.CheckPasswordAsync(user, password) ||
            !await database.CustomerProfiles.AnyAsync(profile => profile.UserId == user.Id, cancellationToken))
        {
            return null;
        }

        var jwt = settings.Value;
        var now = DateTimeOffset.FromUnixTimeSeconds(clock.GetUtcNow().ToUnixTimeSeconds());
        var stamp = await users.GetSecurityStampAsync(user);
        var existing = await database.AccountSessions.FromSqlInterpolated(
            $"SELECT * FROM \"AccountSessions\" WHERE \"Id\" = {loginKey} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (existing is not null && (existing.UserId != user.Id || existing.SecurityStamp != stamp ||
            existing.RevokedAt is not null || existing.ExpiresAt <= clock.GetUtcNow()))
        {
            return null;
        }

        var session = existing ?? new AccountSession
        {
            Id = loginKey,
            UserId = user.Id,
            SecurityStamp = stamp,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(jwt.ExpiryMinutes)
        };
        var token = new JwtSecurityToken(jwt.Issuer, jwt.Audience,
            [new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, session.Id.ToString())],
            session.CreatedAt.UtcDateTime, session.ExpiresAt.UtcDateTime,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)), SecurityAlgorithms.HmacSha256));
        var result = new LoginSession(new JwtSecurityTokenHandler().WriteToken(token), session.ExpiresAt);

        if (existing is null)
        {
            database.AccountSessions.Add(session);

            try
            {
                await database.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "PK_AccountSessions" })
            {
                // A competing account cannot claim or replay another account's login key.
                return null;
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<bool> IsActiveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return await database.AccountSessions.AnyAsync(session => session.Id == sessionId && session.UserId == userId &&
            session.RevokedAt == null && session.ExpiresAt > now &&
            database.Users.Any(user => user.Id == session.UserId && user.SecurityStamp == session.SecurityStamp), cancellationToken);
    }

    public async Task RevokeAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        // Conditional UPDATE serializes competing logouts without changing the first revocation receipt.
        await database.AccountSessions.Where(session => session.Id == sessionId && session.UserId == userId && session.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(session => session.RevokedAt, now), cancellationToken);
    }

    public async Task<CustomerAccount?> GetAccountAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await (from profile in database.CustomerProfiles
                      join user in database.Users on profile.UserId equals user.Id
                      where profile.UserId == userId
                      select new CustomerAccount(user.Id, profile.LegalName, user.Email!, user.EmailConfirmed))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
