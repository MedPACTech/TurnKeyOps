using System.Security.Claims;
using MedInsights.API.Infrastructure;
using MedInsights.Lib;
using MedInsights.Lib.Authorization;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using Moq;

namespace MedInsights.Authorization.Tests;

public sealed class TenantRoleClaimsTransformationTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherTenantId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task TokenRoleCannotSelfGrantTenantAdminWithoutMembership()
    {
        var membershipRepository = new Mock<ITenantMembershipRepository>();
        var transformer = CreateTransformer(membershipRepository);
        var principal = Principal(TenantId, UserId, new Claim(ClaimTypes.Role, TenantRoleCatalog.Admin));

        var transformed = await transformer.TransformAsync(principal);

        Assert.False(transformed.IsInRole(TenantRoleCatalog.Admin));
        Assert.Empty(transformed.FindAll(TurnKeyAuthorizationPolicies.TenantRoleClaimType));
        membershipRepository.Verify(
            x => x.GetByUserIdAsync(EntityKeyPolicy.TenantPartition(TenantId), UserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MembershipLookupIsBoundToTheClaimedTenantPartition()
    {
        var membershipRepository = new Mock<ITenantMembershipRepository>();
        membershipRepository
            .Setup(x => x.GetByUserIdAsync(EntityKeyPolicy.TenantPartition(OtherTenantId), UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantMembership?)null);
        var transformer = CreateTransformer(membershipRepository);

        var transformed = await transformer.TransformAsync(Principal(OtherTenantId, UserId));

        Assert.Empty(transformed.FindAll(TurnKeyAuthorizationPolicies.TenantRoleClaimType));
        membershipRepository.Verify(
            x => x.GetByUserIdAsync(EntityKeyPolicy.TenantPartition(OtherTenantId), UserId, It.IsAny<CancellationToken>()),
            Times.Once);
        membershipRepository.Verify(
            x => x.GetByUserIdAsync(EntityKeyPolicy.TenantPartition(TenantId), UserId, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ActiveMembershipAddsServerResolvedTenantRole()
    {
        var membershipRepository = new Mock<ITenantMembershipRepository>();
        membershipRepository
            .Setup(x => x.GetByUserIdAsync(EntityKeyPolicy.TenantPartition(TenantId), UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantMembership
            {
                TenantId = TenantId,
                UserId = UserId,
                Role = TenantRoleCatalog.Staff,
                MembershipStatus = "Active"
            });
        var roleDirectory = new Mock<IRoleDirectoryService>();
        roleDirectory.Setup(x => x.NormalizeRoleKey(TenantRoleCatalog.Staff)).Returns(TenantRoleCatalog.Staff);
        roleDirectory
            .Setup(x => x.GetRoleAsync(TenantId, TenantRoleCatalog.Staff, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantRoleDefinition { Id = Guid.NewGuid(), Key = TenantRoleCatalog.Staff });
        var transformer = new TenantRoleClaimsTransformation(membershipRepository.Object, roleDirectory.Object);

        var transformed = await transformer.TransformAsync(Principal(TenantId, UserId));

        Assert.True(transformed.IsInRole(TenantRoleCatalog.Staff));
        Assert.Equal(
            TenantRoleCatalog.Staff,
            transformed.FindFirst(TurnKeyAuthorizationPolicies.TenantRoleClaimType)?.Value);
        Assert.Equal("tenant-membership", transformed.FindFirst(TurnKeyAuthorizationPolicies.TenantRoleClaimType)?.Issuer);
    }

    [Fact]
    public async Task RemovedMembershipCannotAuthorizeTenantAccess()
    {
        var membershipRepository = new Mock<ITenantMembershipRepository>();
        membershipRepository
            .Setup(x => x.GetByUserIdAsync(EntityKeyPolicy.TenantPartition(TenantId), UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantMembership
            {
                TenantId = TenantId,
                UserId = UserId,
                Role = TenantRoleCatalog.Admin,
                MembershipStatus = "Removed",
                DateRemoved = DateTime.UtcNow
            });
        var transformer = CreateTransformer(membershipRepository);

        var transformed = await transformer.TransformAsync(Principal(TenantId, UserId));

        Assert.False(transformed.IsInRole(TenantRoleCatalog.Admin));
        Assert.Empty(transformed.FindAll(TurnKeyAuthorizationPolicies.TenantRoleClaimType));
    }

    [Fact]
    public async Task InternalAdminRequiresExplicitIdentityProviderRole()
    {
        var membershipRepository = new Mock<ITenantMembershipRepository>();
        var transformer = CreateTransformer(membershipRepository);
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, UserId.ToString()), new Claim(ClaimTypes.Role, "Internal Admin")],
            "test");

        var transformed = await transformer.TransformAsync(new ClaimsPrincipal(identity));

        Assert.True(transformed.IsInRole(TurnKeyAuthorizationRoles.InternalAdmin));
        Assert.False(transformed.IsInRole(TenantRoleCatalog.Admin));
        membershipRepository.VerifyNoOtherCalls();
    }

    private static TenantRoleClaimsTransformation CreateTransformer(Mock<ITenantMembershipRepository> membershipRepository)
    {
        var roleDirectory = new Mock<IRoleDirectoryService>();
        roleDirectory.Setup(x => x.NormalizeRoleKey(It.IsAny<string>()))
            .Returns((string value) => value.Trim().ToLowerInvariant());
        return new TenantRoleClaimsTransformation(membershipRepository.Object, roleDirectory.Object);
    }

    private static ClaimsPrincipal Principal(Guid tenantId, Guid userId, params Claim[] additionalClaims)
    {
        var claims = new List<Claim>
        {
            new("tenant_id", tenantId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        claims.AddRange(additionalClaims);
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }
}
