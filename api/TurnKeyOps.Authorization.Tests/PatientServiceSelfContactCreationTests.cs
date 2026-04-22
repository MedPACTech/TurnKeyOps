using MedInsights.Authorization.Tests.Infrastructure;
using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Enums;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services;
using MedInsights.Services.Interfaces;
using Moq;

namespace MedInsights.Authorization.Tests;

public sealed class PatientServiceSelfContactCreationTests
{
    [Fact]
    public async Task AddAsync_CreatesSelfContact_WhenMissing()
    {
        var patientRepository = new Mock<IPatientRepository>();
        patientRepository
            .Setup(x => x.SaveAsync(It.IsAny<Patient>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient entity, CancellationToken _) => entity);

        var patientContextService = new Mock<IPatientContextService>();
        var patientContactService = new Mock<IPatientContactService>();
        patientContactService
            .Setup(x => x.GetByPatientAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<PatientContactDto>());
        patientContactService
            .Setup(x => x.AddAsync(It.IsAny<PatientContactDto>()))
            .ReturnsAsync((PatientContactDto dto) => dto);
        var facilityService = new Mock<IFacilityService>();

        var membershipAuthorization = new Mock<ITenantMembershipAuthorizationService>();

        var service = new PatientService(
            patientRepository.Object,
            new TestUserContext
            {
                IsAuthenticated = true,
                TenantId = TestIds.TenantId,
                UserId = TestIds.UserId
            },
            patientContextService.Object,
            patientContactService.Object,
            facilityService.Object,
            membershipAuthorization.Object);

        var created = await service.AddAsync(new PatientDto
        {
            Id = Guid.NewGuid(),
            FirstName = "Jane",
            LastName = "Doe",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Gender = "Female",
            PatientStatus = "Active"
        });

        patientContactService.Verify(x => x.AddAsync(It.Is<PatientContactDto>(dto =>
            dto.PatientId == created.Id &&
            dto.ContactType == ContactType.Self &&
            dto.Relationship == PatientRelationship.Self &&
            dto.IsPrimary &&
            !dto.IsSecondary &&
            dto.FirstName == "Jane" &&
            dto.LastName == "Doe")), Times.Once);
    }

    [Fact]
    public async Task AddAsync_AssignsFacility_WhenCurrentFacilityIdProvided()
    {
        var facilityId = Guid.NewGuid();

        var patientRepository = new Mock<IPatientRepository>();
        patientRepository
            .Setup(x => x.SaveAsync(It.IsAny<Patient>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient entity, CancellationToken _) => entity);
        patientRepository
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync((string pk, string rk, CancellationToken _, bool __) => new Patient
            {
                Id = Guid.Parse(rk),
                PartitionKey = pk,
                RowKey = rk,
                FirstName = "Jane",
                LastName = "Doe",
                DateOfBirth = DateTime.SpecifyKind(new DateTime(1990, 1, 1), DateTimeKind.Utc),
                Gender = "Female",
                PatientStatus = "Active",
                CurrentFacilityId = facilityId,
                CurrentFacilityName = "Test Facility",
                CurrentFacilityStatus = "Admitted"
            });

        var patientContextService = new Mock<IPatientContextService>();
        var patientContactService = new Mock<IPatientContactService>();
        patientContactService
            .Setup(x => x.GetByPatientAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<PatientContactDto>());
        patientContactService
            .Setup(x => x.AddAsync(It.IsAny<PatientContactDto>()))
            .ReturnsAsync((PatientContactDto dto) => dto);

        var facilityService = new Mock<IFacilityService>();
        facilityService
            .Setup(x => x.GetAsync(facilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FacilityDto
            {
                Id = facilityId,
                FacilityName = "Test Facility",
                IsResidential = true
            });
        facilityService
            .Setup(x => x.AdmitPatientAsync(facilityId, It.IsAny<AdmitFacilityPatientDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FacilityPatientAssignmentDto
            {
                Id = Guid.NewGuid(),
                FacilityId = facilityId,
                PatientId = Guid.Empty,
                PatientFirstName = "Jane",
                PatientLastName = "Doe",
                Status = "Admitted",
                AdmitDate = DateTime.UtcNow
            });

        var membershipAuthorization = new Mock<ITenantMembershipAuthorizationService>();

        var service = new PatientService(
            patientRepository.Object,
            new TestUserContext
            {
                IsAuthenticated = true,
                TenantId = TestIds.TenantId,
                UserId = TestIds.UserId
            },
            patientContextService.Object,
            patientContactService.Object,
            facilityService.Object,
            membershipAuthorization.Object);

        var created = await service.AddAsync(new PatientDto
        {
            Id = Guid.NewGuid(),
            FirstName = "Jane",
            LastName = "Doe",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Gender = "Female",
            PatientStatus = "Active",
            CurrentFacilityId = facilityId
        });

        facilityService.Verify(x => x.AdmitPatientAsync(
            facilityId,
            It.Is<AdmitFacilityPatientDto>(dto => dto.PatientId == created.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddAsync_SetsSelfContactCommunication_ForAdultPatients()
    {
        var patientRepository = new Mock<IPatientRepository>();
        patientRepository
            .Setup(x => x.SaveAsync(It.IsAny<Patient>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient entity, CancellationToken _) => entity);

        var patientContextService = new Mock<IPatientContextService>();
        var addedContacts = new List<PatientContactDto>();
        var patientContactService = new Mock<IPatientContactService>();
        patientContactService
            .Setup(x => x.GetByPatientAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<PatientContactDto>());
        patientContactService
            .Setup(x => x.AddAsync(It.IsAny<PatientContactDto>()))
            .Callback<PatientContactDto>(dto => addedContacts.Add(dto))
            .ReturnsAsync((PatientContactDto dto) => dto);
        var facilityService = new Mock<IFacilityService>();
        var membershipAuthorization = new Mock<ITenantMembershipAuthorizationService>();

        var service = new PatientService(
            patientRepository.Object,
            new TestUserContext
            {
                IsAuthenticated = true,
                TenantId = TestIds.TenantId,
                UserId = TestIds.UserId
            },
            patientContextService.Object,
            patientContactService.Object,
            facilityService.Object,
            membershipAuthorization.Object);

        await service.AddAsync(new PatientDto
        {
            Id = Guid.NewGuid(),
            FirstName = "Jane",
            LastName = "Doe",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Gender = "Female",
            PatientStatus = "Active",
            Phone = "614-555-1000",
            Email = "jane@example.com",
            HasHIPAAPermission = true,
            HasBillingPermission = true
        });

        Assert.Single(addedContacts);
        var selfContact = addedContacts[0];
        Assert.Equal(PatientRelationship.Self, selfContact.Relationship);
        Assert.Equal("614-555-1000", selfContact.PrimaryPhone);
        Assert.Equal("jane@example.com", selfContact.Email);
        Assert.True(selfContact.HasHIPAAPermission);
        Assert.True(selfContact.HasBillingPermission);
    }

    [Fact]
    public async Task AddAsync_CreatesPrimaryContact_ForMinorPatientFromPayload()
    {
        var patientRepository = new Mock<IPatientRepository>();
        patientRepository
            .Setup(x => x.SaveAsync(It.IsAny<Patient>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient entity, CancellationToken _) => entity);

        var patientContextService = new Mock<IPatientContextService>();
        var addedContacts = new List<PatientContactDto>();
        var patientContactService = new Mock<IPatientContactService>();
        patientContactService
            .Setup(x => x.GetByPatientAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<PatientContactDto>());
        patientContactService
            .Setup(x => x.AddAsync(It.IsAny<PatientContactDto>()))
            .Callback<PatientContactDto>(dto => addedContacts.Add(dto))
            .ReturnsAsync((PatientContactDto dto) => dto);
        var facilityService = new Mock<IFacilityService>();
        var membershipAuthorization = new Mock<ITenantMembershipAuthorizationService>();

        var service = new PatientService(
            patientRepository.Object,
            new TestUserContext
            {
                IsAuthenticated = true,
                TenantId = TestIds.TenantId,
                UserId = TestIds.UserId
            },
            patientContextService.Object,
            patientContactService.Object,
            facilityService.Object,
            membershipAuthorization.Object);

        await service.AddAsync(new PatientDto
        {
            Id = Guid.NewGuid(),
            FirstName = "Minor",
            LastName = "Patient",
            DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-10),
            Gender = "Female",
            PatientStatus = "Active",
            Phone = "614-555-2000",
            Email = "mom@example.com",
            HasHIPAAPermission = true,
            HasBillingPermission = false,
            PrimaryFirstName = "Sara",
            PrimaryLastName = "Patient",
            Relationship = PatientRelationship.Mother
        });

        Assert.Equal(2, addedContacts.Count);

        var selfContact = addedContacts.Single(c => c.Relationship == PatientRelationship.Self);
        Assert.True(selfContact.IsPrimary);
        Assert.Null(selfContact.PrimaryPhone);
        Assert.Null(selfContact.Email);

        var primaryContact = addedContacts.Single(c => c.Relationship == PatientRelationship.Mother);
        Assert.Equal(ContactType.Emergency, primaryContact.ContactType);
        Assert.True(primaryContact.IsPrimary);
        Assert.Equal("Sara", primaryContact.FirstName);
        Assert.Equal("Patient", primaryContact.LastName);
        Assert.Equal("614-555-2000", primaryContact.PrimaryPhone);
        Assert.Equal("mom@example.com", primaryContact.Email);
        Assert.True(primaryContact.HasHIPAAPermission);
        Assert.False(primaryContact.HasBillingPermission);
    }

    [Fact]
    public async Task AddAsync_Throws_WhenMinorPayloadMissingPrimaryContact()
    {
        var patientRepository = new Mock<IPatientRepository>();
        var patientContextService = new Mock<IPatientContextService>();
        var patientContactService = new Mock<IPatientContactService>();
        var facilityService = new Mock<IFacilityService>();
        var membershipAuthorization = new Mock<ITenantMembershipAuthorizationService>();

        var service = new PatientService(
            patientRepository.Object,
            new TestUserContext
            {
                IsAuthenticated = true,
                TenantId = TestIds.TenantId,
                UserId = TestIds.UserId
            },
            patientContextService.Object,
            patientContactService.Object,
            facilityService.Object,
            membershipAuthorization.Object);

        await Assert.ThrowsAsync<ArgumentException>(() => service.AddAsync(new PatientDto
        {
            Id = Guid.NewGuid(),
            FirstName = "Minor",
            LastName = "Patient",
            DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-10),
            Gender = "Female",
            PatientStatus = "Active"
        }));

        patientRepository.Verify(x => x.SaveAsync(It.IsAny<Patient>(), It.IsAny<CancellationToken>()), Times.Never);
        patientContactService.Verify(x => x.AddAsync(It.IsAny<PatientContactDto>()), Times.Never);
    }

    private static class TestIds
    {
        public static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        public static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    }
}
