using System.Security.Claims;
using IBeam.Identity.Interfaces;
using IBeam.Identity.Models;
using MedInsights.Controllers;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace MedInsights.Authorization.Tests;

public sealed class AuthWorkspacesTests
{
    [Theory]
    [InlineData("Active", false, false, false, true)]
    [InlineData("Inactive", false, false, false, false)]
    [InlineData("Active", true, false, false, false)]
    [InlineData("Active", false, true, false, false)]
    [InlineData("Active", false, false, true, false)]
    public async Task SelectionRequiresActiveMembershipForAuthenticatedUser(string status, bool deleted, bool removed, bool foreignUser, bool allowed)
    {
        var userId = Guid.NewGuid(); var tenantId = Guid.NewGuid();
        var repository = new Mock<ITenantMembershipRepository>();
        repository.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] {
            new TenantMembership { UserId = foreignUser ? Guid.NewGuid() : userId, TenantId = tenantId, MembershipStatus = status, IsDeleted = deleted, DateRemoved = removed ? DateTime.UtcNow : null }
        });
        var selection = new Mock<ITenantSelectionService>();
        selection.Setup(s => s.SelectTenantAsync(It.IsAny<TenantSelectionRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new TokenResult("token", DateTimeOffset.UtcNow.AddHours(1), []));
        var controller = new AuthWorkspacesController(repository.Object, selection.Object) {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "test")) } }
        };
        var result = await controller.Select(new(tenantId), default);
        if (allowed) Assert.IsType<OkObjectResult>(result); else Assert.IsType<ForbidResult>(result);
        selection.Verify(s => s.SelectTenantAsync(It.Is<TenantSelectionRequest>(r => r.UserId == userId && r.TenantId == tenantId && !r.SetAsDefault), It.IsAny<CancellationToken>()), allowed ? Times.Once() : Times.Never());
        Assert.IsType<ForbidResult>(await controller.Select(new(Guid.NewGuid()), default));
    }
}
