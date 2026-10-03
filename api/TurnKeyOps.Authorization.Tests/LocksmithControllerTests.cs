using System.Text.Json;
using MedInsights.Lib;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TurnKeyOps.API.Controllers;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories.Interfaces;

namespace MedInsights.Authorization.Tests;

public sealed class LocksmithControllerTests
{
    private readonly Guid tenantId = Guid.NewGuid();
    private readonly Guid userId = Guid.NewGuid();
    private readonly Guid membershipId = Guid.NewGuid();

    private (LocksmithController Controller, Mock<ITenantMembershipRepository> Members, Mock<ITenantSettingsRepository> Settings) Fixture(string status = "Active", bool otherTenant = false, bool removed = false)
    {
        var user = new Mock<IUserContext>();
        user.SetupGet(value => value.IsAuthenticated).Returns(true);
        user.SetupGet(value => value.TenantId).Returns(tenantId);
        user.SetupGet(value => value.UserId).Returns(userId);
        var members = new Mock<ITenantMembershipRepository>();
        members.Setup(value => value.GetByUserIdAsync(EntityKeyPolicy.TenantPartition(tenantId), userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantMembership { Id = membershipId, TenantId = otherTenant ? Guid.NewGuid() : tenantId, UserId = userId, MembershipStatus = status, DateRemoved = removed ? DateTime.UtcNow : null });
        var settings = new Mock<ITenantSettingsRepository>();
        return (new LocksmithController(user.Object, members.Object, settings.Object) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } }, members, settings);
    }

    [Theory]
    [InlineData("Removed", false, false)]
    [InlineData("Active", true, false)]
    [InlineData("Active", false, true)]
    public async Task DeniesInactiveRemovedOrOtherTenantMembership(string status, bool otherTenant, bool removed)
    {
        var fixture = Fixture(status, otherTenant, removed);
        Assert.IsType<ForbidResult>(await fixture.Controller.Me(default));
        fixture.Settings.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ActiveMemberWithoutConfigurationReceivesNoCapabilities()
    {
        var fixture = Fixture();
        var result = Assert.IsType<OkObjectResult>(await fixture.Controller.Me(default));
        var envelope = JsonSerializer.SerializeToElement(result.Value);
        Assert.Empty(envelope.GetProperty("Data").GetProperty("capabilities").EnumerateArray());
    }

    [Fact]
    public async Task ReturnsOnlyCurrentMembershipCapabilitiesFromCurrentTenant()
    {
        var fixture = Fixture();
        fixture.Settings.Setup(value => value.GetAsync(TurnKeyOps.Lib.Utils.RepositoryKeyHelper.ToTenantPartitionKey(tenantId), "SETTINGS|OPERATIONAL", It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(new TenantSettingsDocument { ValuesJson = JsonSerializer.Serialize(new { locksmith = new { techCapabilities = new Dictionary<string, string[]> { [membershipId.ToString()] = ["residential"] } } }) });
        var result = Assert.IsType<OkObjectResult>(await fixture.Controller.Me(default));
        var data = JsonSerializer.SerializeToElement(result.Value).GetProperty("Data");
        Assert.Equal(tenantId, data.GetProperty("tenantId").GetGuid());
        Assert.Equal(userId, data.GetProperty("userId").GetGuid());
        Assert.Equal("residential", data.GetProperty("capabilities")[0].GetString());
        fixture.Settings.Verify(value => value.GetAsync(TurnKeyOps.Lib.Utils.RepositoryKeyHelper.ToTenantPartitionKey(tenantId), "SETTINGS|OPERATIONAL", It.IsAny<CancellationToken>(), false), Times.Once);
    }
}
