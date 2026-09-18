using System.Net;
using System.Text.Encodings.Web;
using IBeam.Identity.Api.Controllers;
using MedInsights.API.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;

namespace MedInsights.Authorization.Tests;

public sealed class IdentityOtpAuthorizationTests
{
    [Theory]
    [InlineData("/api/auth/startotp", true, HttpStatusCode.NoContent)]
    [InlineData("/api/auth/completeotp", true, HttpStatusCode.NoContent)]
    [InlineData("/api/auth/startotp", false, HttpStatusCode.Unauthorized)]
    public async Task AnonymousOtpRequestPassesAuthorizationOnlyWithExplicitEndpointMetadata(
        string path, bool applyConvention, HttpStatusCode expected)
    {
        using var server = await CreateServer(applyConvention);
        using var response = await server.GetTestClient().PostAsync(path, new StringContent("{}"));
        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task TenantRoleManagementStillRequiresAuthentication()
    {
        using var server = await CreateServer(true);
        using var response = await server.GetTestClient().GetAsync(
            "/api/tenants/11111111-1111-1111-1111-111111111111/roles");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static Task<IHost> CreateServer(bool applyConvention) => new HostBuilder()
        .ConfigureWebHost(web => web.UseTestServer()
        .ConfigureServices(services =>
        {
            services.AddAuthentication("test")
                .AddScheme<AuthenticationSchemeOptions, AnonymousAuthenticationHandler>("test", _ => { });
            services.AddAuthorization(options => options.FallbackPolicy =
                new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
            services.AddControllers(options =>
                {
                    if (applyConvention)
                        options.Conventions.Add(new IdentityOtpAnonymousConvention());
                })
                .AddApplicationPart(typeof(AuthController).Assembly);
        })
        .Configure(app =>
        {
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            // Exercise the actual packaged route metadata without sending OTPs or
            // connecting to production identity/communications services.
            app.Run(context =>
            {
                context.Response.StatusCode = context.GetEndpoint() is null
                    ? StatusCodes.Status404NotFound
                    : StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            });
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }))
        .StartAsync();

    private sealed class AnonymousAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(AuthenticateResult.NoResult());
    }
}
