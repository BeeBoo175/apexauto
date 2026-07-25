using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dealership.IntegrationTests;

public sealed class AuthenticationReadTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task OwnReadsPreserveIdentityDenialsAndPrivateUnavailableResponses()
    {
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var registration = await IntakePhotoFixture.RegisterAsync(client);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(client.DefaultRequestHeaders.Authorization!.Parameter);
        var userId = Guid.Parse(token.Subject);
        var sessionId = Guid.Parse(token.Id);
        var requests = new[]
        {
            (Path: "/api/account", Title: "Account details are temporarily unavailable.", Denied: HttpStatusCode.Forbidden),
            (Path: "/api/account/profile", Title: "Account profile is temporarily unavailable.", Denied: HttpStatusCode.Forbidden),
            (Path: "/api/auth/session", Title: "Session details are temporarily unavailable.", Denied: HttpStatusCode.Unauthorized)
        };

        foreach (var request in requests)
        {
            using var response = await client.GetAsync(request.Path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
        }

        var account = (await client.GetFromJsonAsync<CustomerAccount>("/api/account"))!;
        var profile = (await client.GetFromJsonAsync<AccountProfile>("/api/account/profile"))!;
        var actor = (await client.GetFromJsonAsync<CurrentActor>("/api/auth/session"))!;
        Assert.Equal((userId, registration.Email), (account.UserId, account.Email));
        Assert.Equal((userId, registration.Username), (profile.UserId, profile.Username));
        Assert.Equal(new CurrentActor(userId, AccountRoles.Customer, null, false), actor);

        var reads = new ReadProbe(userId, sessionId);
        using var probeFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICustomerAccountQuery>();
            services.AddSingleton<ICustomerAccountQuery>(reads);
            services.RemoveAll<IAccountProfileQuery>();
            services.AddSingleton<IAccountProfileQuery>(reads);
            services.RemoveAll<IAccountSessionStore>();
            services.AddScoped<IAccountSessionStore>(provider => new SessionReadProbe(
                ActivatorUtilities.CreateInstance<AccountSessionStore>(provider), reads));
        }));
        using var probe = probeFactory.CreateClient();
        probe.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;

        foreach (var request in requests)
        {
            using var response = await probe.GetAsync(request.Path);
            Assert.Equal(request.Denied, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
        }

        reads.Cancel = true;

        foreach (var request in requests)
        {
            using var response = await probe.GetAsync(request.Path);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal(request.Title, problem?.Title);
        }
    }

    private sealed class ReadProbe(Guid expectedUserId, Guid expectedSessionId) : ICustomerAccountQuery, IAccountProfileQuery
    {
        public bool Cancel { get; set; }

        public Task<CustomerAccount?> GetAccountAsync(Guid userId, CancellationToken cancellationToken)
        {
            return ReadAsync<CustomerAccount>(userId, cancellationToken);
        }

        public Task<AccountProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
        {
            return ReadAsync<AccountProfile>(userId, cancellationToken);
        }

        public Task<CurrentActor?> GetActorAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
        {
            Assert.Equal(expectedSessionId, sessionId);
            return ReadAsync<CurrentActor>(userId, cancellationToken);
        }

        private Task<T?> ReadAsync<T>(Guid userId, CancellationToken cancellationToken) where T : class
        {
            Assert.Equal(expectedUserId, userId);
            Assert.True(cancellationToken.CanBeCanceled);

            if (Cancel)
            {
                return Task.FromException<T?>(new OperationCanceledException());
            }

            return Task.FromResult<T?>(null);
        }
    }

    private sealed class SessionReadProbe(IAccountSessionStore sessions, ReadProbe reads) : IAccountSessionStore
    {
        public Task<LoginSession?> LoginAsync(Guid loginKey, string email, string password, CancellationToken cancellationToken)
        {
            return sessions.LoginAsync(loginKey, email, password, cancellationToken);
        }

        public Task<bool> IsActiveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
        {
            return sessions.IsActiveAsync(userId, sessionId, cancellationToken);
        }

        public Task<bool> IsUnverifiedStaffAsync(Guid userId, CancellationToken cancellationToken)
        {
            return sessions.IsUnverifiedStaffAsync(userId, cancellationToken);
        }

        public Task<CurrentActor?> GetActorAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
        {
            return reads.GetActorAsync(userId, sessionId, cancellationToken);
        }

        public Task RevokeAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
        {
            return sessions.RevokeAsync(userId, sessionId, cancellationToken);
        }
    }
}
