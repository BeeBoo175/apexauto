using Dealership.Application.Accounts;
using Dealership.Domain.Accounts;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Persistence;

internal static class PersonRows
{
    // A staff account's display identity: its profile name, or the former-employee label when the profile was erased
    // or never existed. Customer accounts are not staff and are never listed.
    public static IQueryable<StaffPerson> Staff(DealershipDbContext database)
    {
        return database.Users.AsNoTracking().Where(user => user.Role != AccountRoles.Customer).Select(user => new StaffPerson
        {
            UserId = user.Id,
            Name = database.StaffProfiles.Where(profile => profile.UserId == user.Id)
                .Select(profile => profile.Name).FirstOrDefault() ?? PersonLabels.FormerEmployee,
            PictureId = database.AccountPictures.Where(AccountPicture.Current).Where(picture => picture.UserId == user.Id)
                .Select(picture => (Guid?)picture.Id).FirstOrDefault()
        });
    }
}

internal sealed class StaffPerson
{
    public Guid UserId { get; init; }

    public string Name { get; init; } = string.Empty;

    public Guid? PictureId { get; init; }
}
