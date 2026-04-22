using MedInsights.Authorization.Tests.Infrastructure;
using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services;
using Moq;

namespace MedInsights.Authorization.Tests;

public sealed class FacilityServiceAddressLifecycleTests
{
    [Fact]
    public async Task AdmitPatientAsync_SnapshotsPreviousPhysicalAndSetsFacilityAddress()
    {
        var facilityId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var facility = new Facility
        {
            Id = facilityId,
            PartitionKey = EntityKeyPolicy.TenantPartition(TestIds.TenantId),
            RowKey = EntityKeyPolicy.Row(facilityId),
            FacilityName = "Sunrise Facility",
            AddressLine1 = "500 Care Way",
            AddressLine2 = "Suite A",
            City = "Columbus",
            State = "OH",
            PostalCode = "43004",
            IsResidential = true
        };

        var patient = new Patient
        {
            Id = patientId,
            PartitionKey = EntityKeyPolicy.TenantPartition(TestIds.TenantId),
            RowKey = EntityKeyPolicy.Row(patientId),
            FirstName = "Alice",
            LastName = "Jones",
            DateOfBirth = DateTime.SpecifyKind(new DateTime(1980, 1, 1), DateTimeKind.Utc),
            Gender = "Female",
            PhysicalAddressLine1 = "100 Home St",
            PhysicalAddressLine2 = "Apt 2",
            PhysicalCity = "Dayton",
            PhysicalState = "OH",
            PhysicalPostalCode = "45402",
            PhysicalCountry = "US"
        };

        var facilityRepository = new Mock<IFacilityRepository>();
        facilityRepository
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(facility);

        var assignmentRepository = new Mock<IFacilityPatientAssignmentRepository>();
        assignmentRepository
            .Setup(x => x.SaveAsync(It.IsAny<FacilityPatientAssignment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FacilityPatientAssignment entity, CancellationToken _) => entity);

        Patient? savedPatient = null;
        var patientRepository = new Mock<IPatientRepository>();
        patientRepository
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(patient);
        patientRepository
            .Setup(x => x.SaveAsync(It.IsAny<Patient>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient entity, CancellationToken _) =>
            {
                savedPatient = entity;
                return entity;
            });

        var service = new FacilityService(
            facilityRepository.Object,
            assignmentRepository.Object,
            patientRepository.Object,
            CreateUserContext());

        await service.AdmitPatientAsync(facilityId, new AdmitFacilityPatientDto { PatientId = patientId });

        Assert.NotNull(savedPatient);
        Assert.Equal("500 Care Way", savedPatient!.PhysicalAddressLine1);
        Assert.Equal("Columbus", savedPatient.PhysicalCity);
        Assert.Equal("OH", savedPatient.PhysicalState);
        Assert.Equal("43004", savedPatient.PhysicalPostalCode);
        Assert.Equal("100 Home St", savedPatient.PreFacilityPhysicalAddressLine1);
        Assert.Equal("Dayton", savedPatient.PreFacilityPhysicalCity);
    }

    [Fact]
    public async Task AdmitPatientAsync_DoesNotOverwritePhysicalAddress_WhenFacilityIsNotResidential()
    {
        var facilityId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var facility = new Facility
        {
            Id = facilityId,
            PartitionKey = EntityKeyPolicy.TenantPartition(TestIds.TenantId),
            RowKey = EntityKeyPolicy.Row(facilityId),
            FacilityName = "Clinic Location",
            AddressLine1 = "500 Care Way",
            City = "Columbus",
            State = "OH",
            PostalCode = "43004",
            IsResidential = false
        };

        var patient = new Patient
        {
            Id = patientId,
            PartitionKey = EntityKeyPolicy.TenantPartition(TestIds.TenantId),
            RowKey = EntityKeyPolicy.Row(patientId),
            FirstName = "Alice",
            LastName = "Jones",
            DateOfBirth = DateTime.SpecifyKind(new DateTime(1980, 1, 1), DateTimeKind.Utc),
            Gender = "Female",
            PhysicalAddressLine1 = "100 Home St",
            PhysicalAddressLine2 = "Apt 2",
            PhysicalCity = "Dayton",
            PhysicalState = "OH",
            PhysicalPostalCode = "45402",
            PhysicalCountry = "US"
        };

        var facilityRepository = new Mock<IFacilityRepository>();
        facilityRepository
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(facility);

        var assignmentRepository = new Mock<IFacilityPatientAssignmentRepository>();
        assignmentRepository
            .Setup(x => x.SaveAsync(It.IsAny<FacilityPatientAssignment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FacilityPatientAssignment entity, CancellationToken _) => entity);

        Patient? savedPatient = null;
        var patientRepository = new Mock<IPatientRepository>();
        patientRepository
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(patient);
        patientRepository
            .Setup(x => x.SaveAsync(It.IsAny<Patient>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient entity, CancellationToken _) =>
            {
                savedPatient = entity;
                return entity;
            });

        var service = new FacilityService(
            facilityRepository.Object,
            assignmentRepository.Object,
            patientRepository.Object,
            CreateUserContext());

        await service.AdmitPatientAsync(facilityId, new AdmitFacilityPatientDto { PatientId = patientId });

        Assert.NotNull(savedPatient);
        Assert.Equal("100 Home St", savedPatient!.PhysicalAddressLine1);
        Assert.Equal("Dayton", savedPatient.PhysicalCity);
        Assert.Equal("OH", savedPatient.PhysicalState);
        Assert.Equal("45402", savedPatient.PhysicalPostalCode);
        Assert.Null(savedPatient.PreFacilityPhysicalAddressLine1);
        Assert.Equal(facilityId, savedPatient.CurrentFacilityId);
    }

    [Fact]
    public async Task AddAsync_Throws_WhenNumberOfBedsIsNegative()
    {
        var facilityRepository = new Mock<IFacilityRepository>();
        var assignmentRepository = new Mock<IFacilityPatientAssignmentRepository>();
        var patientRepository = new Mock<IPatientRepository>();

        var service = new FacilityService(
            facilityRepository.Object,
            assignmentRepository.Object,
            patientRepository.Object,
            CreateUserContext());

        var dto = new FacilityDto
        {
            FacilityName = "Sunrise Facility",
            NumberOfBeds = -1
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.AddAsync(dto));
    }

    [Fact]
    public async Task DischargePatientAsync_RestoresPreFacilityPhysicalAddress()
    {
        var facilityId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var facility = new Facility
        {
            Id = facilityId,
            PartitionKey = EntityKeyPolicy.TenantPartition(TestIds.TenantId),
            RowKey = EntityKeyPolicy.Row(facilityId),
            FacilityName = "Sunrise Facility",
            IsResidential = true
        };

        var assignment = new FacilityPatientAssignment
        {
            Id = assignmentId,
            FacilityId = facilityId,
            PatientId = patientId,
            PatientFirstName = "Alice",
            PatientLastName = "Jones",
            AdmitDate = DateTime.UtcNow.AddDays(-2),
            Status = "Admitted"
        };

        var patient = new Patient
        {
            Id = patientId,
            PartitionKey = EntityKeyPolicy.TenantPartition(TestIds.TenantId),
            RowKey = EntityKeyPolicy.Row(patientId),
            FirstName = "Alice",
            LastName = "Jones",
            DateOfBirth = DateTime.SpecifyKind(new DateTime(1980, 1, 1), DateTimeKind.Utc),
            Gender = "Female",
            CurrentFacilityId = facilityId,
            CurrentFacilityName = "Sunrise Facility",
            CurrentFacilityStatus = "Admitted",
            PhysicalAddressLine1 = "500 Care Way",
            PhysicalCity = "Columbus",
            PhysicalState = "OH",
            PhysicalPostalCode = "43004",
            PreFacilityPhysicalAddressLine1 = "100 Home St",
            PreFacilityPhysicalAddressLine2 = "Apt 2",
            PreFacilityPhysicalCity = "Dayton",
            PreFacilityPhysicalState = "OH",
            PreFacilityPhysicalPostalCode = "45402",
            PreFacilityPhysicalCountry = "US"
        };

        var facilityRepository = new Mock<IFacilityRepository>();
        facilityRepository
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(facility);

        var assignmentRepository = new Mock<IFacilityPatientAssignmentRepository>();
        assignmentRepository
            .Setup(x => x.GetByFacilityAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([assignment]);
        assignmentRepository
            .Setup(x => x.SaveAsync(It.IsAny<FacilityPatientAssignment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FacilityPatientAssignment entity, CancellationToken _) => entity);

        Patient? savedPatient = null;
        var patientRepository = new Mock<IPatientRepository>();
        patientRepository
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(patient);
        patientRepository
            .Setup(x => x.SaveAsync(It.IsAny<Patient>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient entity, CancellationToken _) =>
            {
                savedPatient = entity;
                return entity;
            });

        var service = new FacilityService(
            facilityRepository.Object,
            assignmentRepository.Object,
            patientRepository.Object,
            CreateUserContext());

        await service.DischargePatientAsync(facilityId, assignmentId, new DischargeFacilityPatientDto());

        Assert.NotNull(savedPatient);
        Assert.Equal("100 Home St", savedPatient!.PhysicalAddressLine1);
        Assert.Equal("Dayton", savedPatient.PhysicalCity);
        Assert.Equal("OH", savedPatient.PhysicalState);
        Assert.Equal("45402", savedPatient.PhysicalPostalCode);
        Assert.Null(savedPatient.PreFacilityPhysicalAddressLine1);
        Assert.Null(savedPatient.CurrentFacilityId);
        Assert.Null(savedPatient.CurrentFacilityName);
        Assert.Null(savedPatient.CurrentFacilityStatus);
    }

    private static TestUserContext CreateUserContext() => new()
    {
        IsAuthenticated = true,
        TenantId = TestIds.TenantId,
        UserId = TestIds.UserId
    };

    private static class TestIds
    {
        public static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        public static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    }
}
