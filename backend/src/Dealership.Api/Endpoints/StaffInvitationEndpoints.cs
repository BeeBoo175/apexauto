using Dealership.Api.Configuration;
using Dealership.Application.Accounts;

namespace Dealership.Api.Endpoints;

public static class StaffInvitationEndpoints
{
    public sealed record EmailRequest(string? Email);

    public sealed record TokenRequest(string? Token);

    public static void MapStaffInvitationEndpoints(this WebApplication app)
    {
        var administration = app.MapGroup("/api/staff-invitations").RequireAuthorization().NoStore();
        administration.MapPost("/", InviteAsync);
        administration.MapPost("/{id:guid}/resends", ResendAsync);
        administration.MapPut("/{id:guid}/email", ChangeEmailAsync);
        administration.MapPost("/{id:guid}/revocations", RevokeAsync);

        // The emailed link carries a token in its fragment; the frontend posts it, never puts it in a URL.
        var activation = app.MapGroup("/api/auth/staff-invitation")
            .RequireRateLimiting(ApiRateLimiting.VerificationPolicy).NoStore();
        activation.MapPost("/preview", PreviewAsync);
        activation.MapPost("/activation", ActivateAsync);
    }

    private static async Task<IResult> InviteAsync(StaffInvitationRequest request, HttpContext context,
        StaffInvitationService invitations, CancellationToken cancellationToken)
    {
        return Map(context, await invitations.InviteAsync(HttpContextActor.ActorId(context), request, cancellationToken),
            Results.Accepted());
    }

    private static async Task<IResult> ResendAsync(Guid id, HttpContext context, StaffInvitationService invitations,
        CancellationToken cancellationToken)
    {
        return Map(context, await invitations.ResendAsync(HttpContextActor.ActorId(context), id, cancellationToken),
            Results.Accepted());
    }

    private static async Task<IResult> ChangeEmailAsync(Guid id, EmailRequest request, HttpContext context,
        StaffInvitationService invitations, CancellationToken cancellationToken)
    {
        return Map(context, await invitations.ChangeEmailAsync(HttpContextActor.ActorId(context), id, request.Email,
            cancellationToken), Results.Accepted());
    }

    private static async Task<IResult> RevokeAsync(Guid id, HttpContext context, StaffInvitationService invitations,
        CancellationToken cancellationToken)
    {
        return Map(context, await invitations.RevokeAsync(HttpContextActor.ActorId(context), id, cancellationToken),
            Results.NoContent());
    }

    private static async Task<IResult> PreviewAsync(TokenRequest request,
        StaffInvitationService invitations, CancellationToken cancellationToken)
    {
        return await BoundedRead.RunAsync(async token =>
        {
            var result = await invitations.ReadPublicAsync(request.Token, token);
            return result.Invitation is { } invitation ? Results.Ok(invitation) : Results.NotFound();
        }, "Staff invitation details are temporarily unavailable.", cancellationToken);
    }

    private static async Task<IResult> ActivateAsync(StaffInvitationActivation request, HttpContext context,
        StaffInvitationService invitations, CancellationToken cancellationToken)
    {
        return Map(context, await invitations.ActivateAsync(request, cancellationToken), Results.NoContent());
    }

    private static IResult Map(HttpContext context, StaffInvitationResult result, IResult success)
    {
        if (result.Status == StaffInvitationStatus.RateLimited && result.RetryAfter is { } retry)
        {
            context.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retry.TotalSeconds))
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return result.Status switch
        {
            StaffInvitationStatus.Succeeded => success,
            StaffInvitationStatus.Forbidden => Results.Forbid(),
            StaffInvitationStatus.NotFound => Results.NotFound(),
            StaffInvitationStatus.EmailTaken => Results.Conflict(new { code = "email-taken" }),
            StaffInvitationStatus.UsernameTaken => Results.Conflict(new { code = "username-taken" }),
            StaffInvitationStatus.Unavailable => Results.Conflict(new { code = "unavailable" }),
            StaffInvitationStatus.RateLimited => Results.StatusCode(StatusCodes.Status429TooManyRequests),
            _ => Results.BadRequest()
        };
    }
}
