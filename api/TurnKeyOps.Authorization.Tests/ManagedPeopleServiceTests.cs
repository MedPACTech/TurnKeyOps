using Azure;
using Azure.Data.Tables;
using IBeam.Identity.Interfaces;
using IBeam.Repositories.AzureTables;
using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services;
using MedInsights.Services.Interfaces;
using Moq;

namespace MedInsights.Authorization.Tests;
public sealed class ManagedPeopleServiceTests
{
    [Fact] public async Task CrossTenantUserCannotBeEditedOrArchived() {
        var f=new Fixture();
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>f.Service.SaveAsync(f.Target,new() {FirstName="Test"},default));
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>f.Service.ArchiveAsync(f.Target,"",default));
        f.Profiles.Verify(p=>p.UpdateAsync(It.IsAny<Guid>(),It.IsAny<UserProfile>(),It.IsAny<TableUpdateMode>(),It.IsAny<ETag>(),It.IsAny<CancellationToken>()),Times.Never);
    }
    [Fact] public async Task OwnerAndSelfCannotBeArchived() {
        var f=new Fixture();f.TargetMember.IsOwner=true;f.Existing();
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.ArchiveAsync(f.Target,"v1",default));
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.ArchiveAsync(f.Actor,"v1",default));
        f.MembershipService.Verify(m=>m.RemoveAsync(It.IsAny<Guid>(),It.IsAny<CancellationToken>()),Times.Never);
    }
    [Fact] public async Task StaleEditCannotOverwritePermissions() {
        var f=new Fixture();f.Existing();
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.SaveAsync(f.Target,new() {FirstName="Test",ExpectedVersion="old"},default));
        f.Profiles.Verify(p=>p.UpdateAsync(It.IsAny<Guid>(),It.IsAny<UserProfile>(),It.IsAny<TableUpdateMode>(),It.IsAny<ETag>(),It.IsAny<CancellationToken>()),Times.Never);
    }
    [Fact] public async Task SavingProfilePreservesIdentityAndVerificationContact() {
        var f=new Fixture();f.Existing();
        await f.Service.SaveAsync(f.Target,new() {FirstName="Updated",ExpectedVersion="v1",ContactEmail="business@example.com",ProfileTypes=["customer","employee"],ModulePermissions=["jobs.read"]},default);
        Assert.Equal(f.Target,f.Profile.ApplicationUserId);
        Assert.Equal("login@example.com",f.Profile.PrimaryEmail);
        Assert.Equal("business@example.com",f.Profile.ContactEmail);
        Assert.Equal(["jobs.read"],f.Profile.ModulePermissions!);
        f.Identities.VerifyNoOtherCalls();
    }
    [Fact] public async Task ArchiveRemovesMembershipAndSoftDeletesProfile() {
        var f=new Fixture();f.Existing();
        await f.Service.ArchiveAsync(f.Target,"v1",default);
        f.MembershipService.Verify(m=>m.RemoveAsync(f.TargetMember.Id,It.IsAny<CancellationToken>()),Times.Once);
        Assert.True(f.Profile.IsDeleted);Assert.False(f.Profile.IsActive);
    }
    [Fact] public async Task RestoreDoesNotRegrantMembership() {
        var f=new Fixture();f.Existing();f.Profile.IsDeleted=true;f.Profile.IsActive=false;
        await f.Service.RestoreAsync(f.Target,"v1",default);
        Assert.False(f.Profile.IsDeleted);Assert.True(f.Profile.IsActive);
        f.MembershipService.VerifyNoOtherCalls();
    }
    [Fact] public async Task CannotLinkCustomerFromDifferentCompany() {
        var f=new Fixture();f.Existing();
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.SaveAsync(f.Target,new() {FirstName="Test",ExpectedVersion="v1",CustomerId=Guid.NewGuid()},default));
    }
    [Fact] public async Task NonOwnerCannotPromoteUserToOwner() {
        var f=new Fixture(); f.Existing();
        f.Members.Setup(m=>m.GetByUserIdAsync(EntityKeyPolicy.TenantPartition(f.Tenant),f.Actor,It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantMembership {Role="admin",MembershipStatus="Active"});
        await Assert.ThrowsAsync<ForbiddenAccessException>(()=>f.Service.UpdateRoleAsync(f.Target,"owner",default));
        f.MembershipService.VerifyNoOtherCalls();
    }
    [Fact] public async Task NonOwnerCannotResetAnotherUserToUnlimitedRoleDefaults() {
        var f=new Fixture();f.Existing();
        f.Members.Setup(m=>m.GetByUserIdAsync(EntityKeyPolicy.TenantPartition(f.Tenant),f.Actor,It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantMembership {Role="admin",MembershipStatus="Active"});
        await Assert.ThrowsAsync<ForbiddenAccessException>(()=>f.Service.SaveAsync(f.Target,new() {FirstName="Test",ProfileTypes=["employee"],ExpectedVersion="v1",ModulePermissions=null},default));
    }
    [Fact] public async Task IdentityLinkDoesNotOverwriteAnArchivedProfile() {
        var repository=new Mock<IUserProfileRepository>();
        var tenant=Guid.NewGuid();var id=Guid.NewGuid();
        var profile=new UserProfile {Id=id,IsActive=false,IsDeleted=true,FirstName="Preserved",ModulePermissions=["jobs.read"]};
        repository.Setup(r=>r.GetAsync(EntityKeyPolicy.TenantPartition(tenant),EntityKeyPolicy.Row(id),It.IsAny<CancellationToken>(),true)).ReturnsAsync(profile);
        var service=new UserProfileService(new Mock<IUserContext>().Object,repository.Object);
        await service.EnsureProfileExistsAsync(tenant,id);
        await service.CreateUserProfileAsync(tenant,id);
        repository.Verify(r=>r.SaveAsync(It.IsAny<UserProfile>(),It.IsAny<CancellationToken>()),Times.Never);
    }
    private sealed class Fixture {
        public Guid Actor=Guid.NewGuid(),Target=Guid.NewGuid(),Tenant=Guid.NewGuid();
        public Mock<IAzureTablesRepositoryStore<UserProfile>> Profiles=new();
        public Mock<ITenantMembershipRepository> Members=new();
        public Mock<IIdentityUserStore> Identities=new();
        public Mock<ITenantMembershipService> MembershipService=new();
        public TenantMembership TargetMember=new() {Id=Guid.NewGuid(),Role="staff",MembershipStatus="Active"};
        public UserProfile Profile;
        public ManagedPeopleService Service;
        public Fixture() {
            var context=new Mock<IUserContext>();context.SetupGet(x=>x.IsAuthenticated).Returns(true);context.SetupGet(x=>x.UserId).Returns(Actor);context.SetupGet(x=>x.TenantId).Returns(Tenant);
            Members.Setup(m=>m.GetByUserIdAsync(EntityKeyPolicy.TenantPartition(Tenant),Actor,It.IsAny<CancellationToken>())).ReturnsAsync(new TenantMembership {Role="owner",IsOwner=true,MembershipStatus="Active"});
            var access=new UserModuleAccessService(Members.Object,Profiles.Object,context.Object);
            Profile=new() {Id=Target,ApplicationUserId=Target,PartitionKey=EntityKeyPolicy.TenantPartition(Tenant),RowKey=EntityKeyPolicy.Row(Target),IsActive=true,PrimaryEmail="login@example.com",ETag=new("v1")};
            Service=new(Profiles.Object,new Mock<IAzureTablesRepositoryStore<TurnKeyOps.Lib.Entities.Customer>>().Object,Members.Object,Identities.Object,context.Object,access,MembershipService.Object,new Mock<IAuditService>().Object,new Mock<IInviteService>().Object,new Mock<ITenantRoleStore>().Object);
        }
        public void Existing() {
            Profiles.Setup(p=>p.GetByKeysAsync(Profile.PartitionKey,Profile.RowKey,It.IsAny<CancellationToken>())).ReturnsAsync(Profile);
            Members.Setup(m=>m.GetByUserIdAsync(Profile.PartitionKey,Target,It.IsAny<CancellationToken>())).ReturnsAsync(TargetMember);
        }
    }
}
