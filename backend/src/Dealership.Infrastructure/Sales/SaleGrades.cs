using Dealership.Domain.Inspections;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Sales;

internal static class SaleGrades
{
    // The grade signed for the listing the customer reserved, which decides the warranties the sale may offer.
    public static async Task<InspectionGrade?> SignedGradeAsync(DealershipDbContext database, Guid saleId,
        CancellationToken cancellationToken)
    {
        var signedRevisionId = await (from reservation in database.Reservations.AsNoTracking()
                                      join publication in database.ListingPublications.AsNoTracking()
                                          on reservation.PublicationId equals publication.Id
                                      where reservation.Id == saleId
                                      select publication.SignedRevisionId).SingleAsync(cancellationToken);
        var signed = await database.InspectionSignedRevisions.AsNoTracking()
            .SingleAsync(item => item.Id == signedRevisionId, cancellationToken);
        return signed.Report().Assessment.Grade;
    }
}
