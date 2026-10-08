using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Azure;
using MedInsights.Lib.Authorization;
using MedInsights.Lib.Entities;
using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;
using TurnKeyOps.API.Controllers;
using TurnKeyOps.Lib.Configurations;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;
namespace MedInsights.Authorization.Tests;
public sealed class PortalHttpTests
{
    private static readonly Guid Tenant=Guid.NewGuid(),User=Guid.NewGuid(),Customer=Guid.NewGuid();
    private static readonly SymmetricSecurityKey Key=new(Encoding.UTF8.GetBytes("portal-test-key-at-least-32-bytes-long-test-only"));
    private static async Task<WebApplication> Host()
    {
        var state=new PortalAccessState{Configuration=new(){Enabled=true},Grants=[new(){UserId=User,CustomerId=Customer,RecordId=Customer}]};
        var store=new Mock<IPortalAccessStore>();store.Setup(s=>s.ReadAsync(Tenant,It.IsAny<CancellationToken>())).ReturnsAsync(state);
        var profiles=new Mock<IManagedProfileStore>();profiles.Setup(p=>p.GetByKeysAsync(RepositoryKeyHelper.ToTenantPartitionKey(Tenant),RepositoryKeyHelper.ToRowKey(User),It.IsAny<CancellationToken>())).ReturnsAsync(new UserProfile{ApplicationUserId=User,CustomerId=Customer,PartitionKey=RepositoryKeyHelper.ToTenantPartitionKey(Tenant),ProfileTypes=["Customer"],IsActive=true,ModulePermissions=[]});
        var access=new PortalAccessService(store.Object,profiles.Object,Mock.Of<IAuditService>());
        var resolver=new Mock<IQuoteRequestTenantResolver>();resolver.Setup(r=>r.Resolve("bdr")).Returns(new QuoteRequestTenantDefinition{TenantId=Tenant});
        var builder=WebApplication.CreateBuilder();builder.WebHost.UseTestServer();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o=>o.TokenValidationParameters=new(){ValidateIssuer=true,ValidIssuer="portal-test",ValidateAudience=true,ValidAudience="tko-api",ValidateIssuerSigningKey=true,IssuerSigningKey=Key,ValidateLifetime=true,ClockSkew=TimeSpan.Zero});
        builder.Services.AddAuthorization(o=>o.AddPolicy(TurnKeyAuthorizationPolicies.AuthenticatedSession,p=>p.RequireAuthenticatedUser()));
        builder.Services.AddControllers().AddApplicationPart(typeof(PortalController).Assembly).AddControllersAsServices();
        builder.Services.AddTransient(_=>new PortalController(access,null!,null!,null!,resolver.Object));builder.Services.AddTransient(_=>new PortalIdentityController(access,resolver.Object));
        var app=builder.Build();app.UseAuthentication();app.UseAuthorization();app.MapControllers();await app.StartAsync();return app;
    }
    private static string Token(Guid user,bool expired=false,string audience="tko-api")=>new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("portal-test",audience,[new Claim("sub",user.ToString())],DateTime.UtcNow.AddHours(-2),DateTime.UtcNow.AddMinutes(expired?-1:10),new SigningCredentials(Key,SecurityAlgorithms.HmacSha256)));
    [Fact]public async Task HttpIdentityExchangeValidatesSignatureExpiryAudienceAndEntitlementWithoutEmployeeRole()
    {
        await using var host=await Host();var client=host.GetTestClient();
        foreach(var token in new[]{"forged",Token(User,true),Token(User,audience:"other"),Token(Guid.NewGuid())}){
            client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/portal-identity/bdr/session",new{})).StatusCode);
        }
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",Token(User));
        var response=await client.PostAsJsonAsync("/api/portal-identity/bdr/session",new{});Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var session=await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();Assert.Equal(64,session.GetProperty("token").GetString()!.Length);
    }
    [Fact]public async Task EveryCustomerDataSurfaceRejectsMissingPortalSessionEvenWithEmployeeBearer()
    {
        await using var host=await Host();var client=host.GetTestClient();client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",Token(User));
        var id=Guid.NewGuid();var root="/api/portal/bdr";
        foreach(var path in new[]{root,$"{root}/work/job/{id}",$"{root}/messages/job/{id}",$"{root}/files/job/{id}/{id}",$"{root}/messages/job/{id}/files/{id}"})Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync(path)).StatusCode);
        foreach(var path in new[]{$"{root}/logout",$"{root}/jobs/{id}",$"{root}/appointments/{id}",$"{root}/proposals/{id}/accept",$"{root}/messages/job/{id}",$"{root}/work/job/{id}/explain"})Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync(path,new{})).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync($"{root}/preferences",new[]{"email"})).StatusCode);
    }
}
