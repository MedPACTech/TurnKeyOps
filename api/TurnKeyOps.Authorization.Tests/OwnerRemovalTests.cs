using MedInsights.Lib;
using MedInsights.Lib.Authorization;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services;
using MedInsights.Services.Interfaces;
using Moq;

namespace MedInsights.Authorization.Tests;

public sealed class OwnerRemovalTests
{
    [Fact] public async Task RemovingAnotherOwnerRevokesLiveMembershipAndReleasesTheirSeat() {
        var f=new Fixture();
        await f.Service.RemoveAsync(f.Target.Id);
        Assert.Equal("Removed",f.Target.MembershipStatus);Assert.NotNull(f.Target.DateRemoved);
        Assert.Empty(UserModulePermissions.Resolve(f.Target,null));
        f.Seats.Verify(s=>s.ReleaseSeatAsync(f.Tenant,"Assigned",It.IsAny<CancellationToken>()),Times.Once);
    }
    [Theory] [InlineData("admin",true)] [InlineData("owner",false)]
    public async Task NonOwnerOrRemovedOwnerCannotRemoveAnotherOwner(string role,bool active) {
        var f=new Fixture();f.Actor.Role=role;f.Actor.IsOwner=role=="owner";if(!active)f.Actor.DateRemoved=DateTime.UtcNow;
        await Assert.ThrowsAsync<ForbiddenAccessException>(()=>f.Service.RemoveAsync(f.Target.Id));
        Assert.Null(f.Target.DateRemoved);f.Seats.VerifyNoOtherCalls();
    }
    [Fact] public async Task OwnerCannotRemoveTheirOwnMembership() {
        var f=new Fixture();f.Target.UserId=f.Actor.UserId;
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.RemoveAsync(f.Target.Id));
        Assert.Null(f.Target.DateRemoved);f.Seats.VerifyNoOtherCalls();
    }
    private sealed class Fixture {
        public Guid Tenant=Guid.NewGuid();
        public TenantMembership Actor=new() {UserId=Guid.NewGuid(),Role="owner",IsOwner=true,MembershipStatus="Active"};
        public TenantMembership Target=new() {Id=Guid.NewGuid(),UserId=Guid.NewGuid(),Role="owner",IsOwner=true,MembershipStatus="Active",SeatStatus="Assigned"};
        public Mock<ITenantSeatEntitlementService> Seats=new();
        public TenantMembershipService Service;
        public Fixture() {
            var repo=new Mock<ITenantMembershipRepository>();var context=new Mock<IUserContext>();
            context.SetupGet(c=>c.IsAuthenticated).Returns(true);context.SetupGet(c=>c.TenantId).Returns(Tenant);context.SetupGet(c=>c.UserId).Returns(Actor.UserId);
            repo.Setup(r=>r.GetAsync(EntityKeyPolicy.TenantPartition(Tenant),EntityKeyPolicy.Row(Target.Id),It.IsAny<CancellationToken>(),false)).ReturnsAsync(Target);
            repo.Setup(r=>r.GetByUserIdAsync(EntityKeyPolicy.TenantPartition(Tenant),Actor.UserId,It.IsAny<CancellationToken>())).ReturnsAsync(Actor);
            Service=new(repo.Object,Seats.Object,new Mock<IInviteService>().Object,context.Object,new Mock<IAuditService>().Object,new Mock<ITenantMembershipAuthorizationService>().Object,new Mock<IRoleDirectoryService>().Object);
        }
    }
}
