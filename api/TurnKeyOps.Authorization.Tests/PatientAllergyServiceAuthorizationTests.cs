using MedInsights.Authorization.Tests.Infrastructure;
using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services;
using MedInsights.Services.Interfaces;
using Moq;

namespace MedInsights.Authorization.Tests;

public sealed class PatientAllergyServiceAuthorizationTests
{
    [Fact]
    public async Task GetByPatientAsync_DoesNotInvokeRoleAccessEnforcement()
    {
        var patientId = Guid.NewGuid();
        var repo = new Mock<IPatientAllergyRepository>();
        repo.Setup(x => x.GetByPatientAsync(It.IsAny<string>()))
            .ReturnsAsync([
                new PatientAllergy
                {
                    Id = Guid.NewGuid(),
                    PatientId = patientId,
                    PartitionKey = EntityKeyPolicy.TenantPatientPartition(TestIds.TenantId, patientId),
                    RowKey = Guid.NewGuid().ToString("D"),
                    AllergyType = "Medication",
                    Description = "Penicillin",
                    DateNoted = DateTime.UtcNow
                }
            ]);

        var access = new Mock<IRoleAccessService>();

        var service = CreateService(repo.Object, access.Object);

        var results = await service.GetByPatientAsync(patientId);

        Assert.Single(results);
        access.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AddAsync_ThrowsUnauthorizedWhenUserIsNotAuthenticated()
    {
        var repo = new Mock<IPatientAllergyRepository>();
        var access = new Mock<IRoleAccessService>();

        var service = CreateService(repo.Object, access.Object, isAuthenticated: false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.AddAsync(CreateDto()));
        repo.Verify(x => x.SaveAsync(It.IsAny<PatientAllergy>(), It.IsAny<CancellationToken>()), Times.Never);
        access.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AddAsync_SevereAllergy_DoesNotRequireCascadePermission()
    {
        var repo = new Mock<IPatientAllergyRepository>();
        repo.Setup(x => x.SaveAsync(It.IsAny<PatientAllergy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PatientAllergy entity, CancellationToken _) => entity);

        var access = new Mock<IRoleAccessService>();

        var service = CreateService(repo.Object, access.Object);

        var created = await service.AddAsync(CreateDto("Severe"));

        Assert.NotEqual(Guid.Empty, created.Id);
        repo.Verify(x => x.SaveAsync(It.IsAny<PatientAllergy>(), It.IsAny<CancellationToken>()), Times.Once);
        access.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesWithoutPermissionChecks()
    {
        var dto = CreateDto();
        PatientAllergy? saved = null;
        var existing = new PatientAllergy
        {
            Id = dto.Id,
            PatientId = dto.PatientId,
            PartitionKey = EntityKeyPolicy.TenantPatientPartition(TestIds.TenantId, dto.PatientId),
            RowKey = EntityKeyPolicy.Row(dto.Id),
            AllergyType = dto.AllergyType,
            Description = dto.Description,
            Severity = dto.Severity,
            Reaction = dto.Reaction,
            DateNoted = dto.DateNoted
        };

        var repo = new Mock<IPatientAllergyRepository>();
        repo.Setup(x => x.GetAsync(existing.PartitionKey, existing.RowKey, It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(existing);
        repo.Setup(x => x.SaveAsync(It.IsAny<PatientAllergy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PatientAllergy entity, CancellationToken _) =>
            {
                saved = entity;
                return entity;
            });

        var access = new Mock<IRoleAccessService>();

        var service = CreateService(repo.Object, access.Object);

        await service.DeleteAsync(dto);

        Assert.NotNull(saved);
        Assert.True(saved!.IsDeleted);
        access.VerifyNoOtherCalls();
    }

    private static PatientAllergyService CreateService(
        IPatientAllergyRepository repository,
        IRoleAccessService accessService,
        bool isAuthenticated = true)
        => new(repository, new TestUserContext
        {
            IsAuthenticated = isAuthenticated,
            TenantId = TestIds.TenantId,
            UserId = TestIds.UserId
        }, accessService);

    private static PatientAllergyDto CreateDto(string? severity = null) => new()
    {
        Id = Guid.NewGuid(),
        PatientId = Guid.NewGuid(),
        AllergyType = "Medication",
        Severity = severity,
        Description = "Penicillin",
        Reaction = "Rash",
        DateNoted = DateTime.UtcNow
    };

    private static class TestIds
    {
        public static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        public static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    }
}
