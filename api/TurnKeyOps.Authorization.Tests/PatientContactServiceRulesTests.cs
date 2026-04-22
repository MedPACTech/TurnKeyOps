using MedInsights.Authorization.Tests.Infrastructure;
using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Enums;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services;
using Moq;

namespace MedInsights.Authorization.Tests;

public sealed class PatientContactServiceRulesTests
{
    [Fact]
    public async Task AddAsync_FirstContactMustBeSelf()
    {
        var repository = new Mock<IPatientContactRepository>();
        repository
            .Setup(x => x.GetByPatientAsync(It.IsAny<string>()))
            .ReturnsAsync(Array.Empty<PatientContact>());

        var service = CreateService(repository.Object);

        await Assert.ThrowsAsync<ArgumentException>(() => service.AddAsync(CreateDto(PatientRelationship.Mother)));
    }

    [Fact]
    public async Task AddAsync_CannotBeBothPrimaryAndSecondary()
    {
        var repository = new Mock<IPatientContactRepository>();
        var service = CreateService(repository.Object);
        var dto = CreateDto(PatientRelationship.Self);
        dto.IsPrimary = true;
        dto.IsSecondary = true;

        await Assert.ThrowsAsync<ArgumentException>(() => service.AddAsync(dto));
    }

    [Fact]
    public async Task AddAsync_PrimaryDemotesExistingPrimary()
    {
        var patientId = Guid.NewGuid();
        var existingPrimary = new PatientContact
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            PartitionKey = EntityKeyPolicy.TenantPatientPartition(TestIds.TenantId, patientId),
            RowKey = Guid.NewGuid().ToString("D"),
            Relationship = PatientRelationship.Self,
            ContactType = ContactType.Self,
            IsPrimary = true,
            FirstName = "Self",
            LastName = "Person"
        };

        var repository = new Mock<IPatientContactRepository>();
        repository
            .Setup(x => x.GetByPatientAsync(It.IsAny<string>()))
            .ReturnsAsync([existingPrimary]);
        repository
            .Setup(x => x.SaveAsync(It.IsAny<PatientContact>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PatientContact entity, CancellationToken _) => entity);

        var service = CreateService(repository.Object);
        var dto = CreateDto(PatientRelationship.Spouse, patientId);
        dto.IsPrimary = true;
        dto.IsSecondary = false;

        await service.AddAsync(dto);

        repository.Verify(x => x.SaveAsync(It.Is<PatientContact>(c => c.Id == existingPrimary.Id && !c.IsPrimary), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddAsync_RequiresOtherRelationshipText_WhenRelationshipIsOther()
    {
        var repository = new Mock<IPatientContactRepository>();
        repository
            .Setup(x => x.GetByPatientAsync(It.IsAny<string>()))
            .ReturnsAsync(Array.Empty<PatientContact>());

        var service = CreateService(repository.Object);
        var dto = CreateDto(PatientRelationship.Other);
        dto.OtherRelationship = null;

        await Assert.ThrowsAsync<ArgumentException>(() => service.AddAsync(dto));
    }

    private static PatientContactService CreateService(IPatientContactRepository repository)
        => new(repository, new TestUserContext
        {
            IsAuthenticated = true,
            TenantId = TestIds.TenantId,
            UserId = TestIds.UserId
        });

    private static PatientContactDto CreateDto(PatientRelationship relationship, Guid? patientId = null) => new()
    {
        Id = Guid.NewGuid(),
        PatientId = patientId ?? Guid.NewGuid(),
        ContactType = relationship == PatientRelationship.Self ? ContactType.Self : ContactType.Other,
        Relationship = relationship,
        IsPrimary = true,
        IsSecondary = false,
        FirstName = "John",
        LastName = "Smith"
    };

    private static class TestIds
    {
        public static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        public static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    }
}
