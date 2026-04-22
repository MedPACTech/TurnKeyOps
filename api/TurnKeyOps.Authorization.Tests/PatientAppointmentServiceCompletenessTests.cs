using MedInsights.Authorization.Tests.Infrastructure;
using MedInsights.Lib;
using MedInsights.Lib.Configurations;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Enums;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services;
using MedInsights.Services.Interfaces;
using Microsoft.Extensions.Options;
using Moq;

namespace MedInsights.Authorization.Tests;

public sealed class PatientAppointmentServiceCompletenessTests
{
    [Fact]
    public async Task AddAsync_SoftMode_AllowsMissingHomeVisitAddress_WithWarning()
    {
        var repository = CreateRepository();
        var appointmentTypeRepository = CreateAppointmentTypeRepository();
        var patientService = CreatePatientService(new DateOnly(1988, 1, 1));
        var contactService = CreateContactService(primaryRelationship: PatientRelationship.Self);

        var service = new PatientAppointmentService(
            repository.Object,
            appointmentTypeRepository.Object,
            CreateUserContext(),
            patientService.Object,
            contactService.Object,
            Options.Create(new AppointmentDataCompletenessSettings { ValidationMode = AppointmentValidationMode.Soft }));

        var dto = CreateAppointmentDto();
        dto.AppointmentLocation = AppointmentLocation.Patient_Home;
        dto.VisitAddressLine1 = string.Empty;

        var result = await service.AddAsync(dto);

        Assert.Contains(result.ValidationWarnings, x => x.Contains("Patient home appointments require visit address line 1", StringComparison.OrdinalIgnoreCase));
        repository.Verify(x => x.SaveAsync(It.IsAny<PatientAppointment>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddAsync_StrictMode_RejectsMissingHomeVisitAddress()
    {
        var repository = CreateRepository();
        var appointmentTypeRepository = CreateAppointmentTypeRepository();
        var patientService = CreatePatientService(new DateOnly(1988, 1, 1));
        var contactService = CreateContactService(primaryRelationship: PatientRelationship.Self);

        var service = new PatientAppointmentService(
            repository.Object,
            appointmentTypeRepository.Object,
            CreateUserContext(),
            patientService.Object,
            contactService.Object,
            Options.Create(new AppointmentDataCompletenessSettings { ValidationMode = AppointmentValidationMode.Strict }));

        var dto = CreateAppointmentDto();
        dto.AppointmentLocation = AppointmentLocation.Patient_Home;
        dto.VisitAddressLine1 = string.Empty;

        await Assert.ThrowsAsync<ArgumentException>(() => service.AddAsync(dto));

        repository.Verify(x => x.SaveAsync(It.IsAny<PatientAppointment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddAsync_PopulatesPrimaryContactSnapshot_AndMinorWarning()
    {
        var repository = CreateRepository();
        var appointmentTypeRepository = CreateAppointmentTypeRepository();
        var patientService = CreatePatientService(new DateOnly(DateTime.UtcNow.Year - 10, 6, 1));
        var contactService = CreateContactService(primaryRelationship: PatientRelationship.Self);

        var service = new PatientAppointmentService(
            repository.Object,
            appointmentTypeRepository.Object,
            CreateUserContext(),
            patientService.Object,
            contactService.Object,
            Options.Create(new AppointmentDataCompletenessSettings { ValidationMode = AppointmentValidationMode.Soft }));

        var dto = CreateAppointmentDto();
        var result = await service.AddAsync(dto);

        Assert.NotNull(result.PrimaryContactId);
        Assert.Equal("Primary", result.PrimaryContactFirstName);
        Assert.Equal("Contact", result.PrimaryContactLastName);
        Assert.Equal("614-555-1212", result.PrimaryContactPhone);
        Assert.Contains(result.ValidationWarnings, x => x.Contains("Minor patient has Self as primary contact", StringComparison.OrdinalIgnoreCase));
    }

    private static Mock<IPatientAppointmentRepository> CreateRepository()
    {
        var repository = new Mock<IPatientAppointmentRepository>();
        repository
            .Setup(x => x.SaveAsync(It.IsAny<PatientAppointment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PatientAppointment entity, CancellationToken _) => entity);
        return repository;
    }

    private static Mock<IPatientService> CreatePatientService(DateOnly dob)
    {
        var patientService = new Mock<IPatientService>();
        patientService
            .Setup(x => x.GetAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => new PatientDto
            {
                Id = id,
                PatientId = id,
                FirstName = "Patient",
                LastName = "One",
                DateOfBirth = dob,
                Gender = "Female",
                PatientStatus = "Active"
            });
        return patientService;
    }

    private static Mock<IPatientContactService> CreateContactService(PatientRelationship primaryRelationship)
    {
        var contactId = Guid.Parse("9d017325-c5b2-4c52-b432-f0bdb6b1677e");
        var contactService = new Mock<IPatientContactService>();
        contactService
            .Setup(x => x.GetByPatientAsync(It.IsAny<Guid>()))
            .ReturnsAsync([
                new PatientContactDto
                {
                    Id = contactId,
                    PatientId = Guid.Empty,
                    ContactType = ContactType.Self,
                    Relationship = primaryRelationship,
                    IsPrimary = true,
                    IsSecondary = false,
                    FirstName = "Primary",
                    LastName = "Contact",
                    PrimaryPhone = "614-555-1212",
                    Email = "primary@example.com"
                }
            ]);
        return contactService;
    }

    private static TestUserContext CreateUserContext() => new()
    {
        IsAuthenticated = true,
        TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")
    };

    private static PatientAppointmentDto CreateAppointmentDto() => new()
    {
        Id = Guid.NewGuid(),
        PatientId = Guid.NewGuid(),
        PatientFirstName = "Patient",
        PatientLastName = "One",
        AppointmentTypeId = Guid.Parse("40f8ca77-e5a8-45ef-b2b0-0e8f515d3d74"),
        AppointmentType = "Routine Checkup",
        AppointmentLocation = AppointmentLocation.Clinic,
        AppointmentStatus = AppointmentStatus.Scheduled,
        AppointmentStartTime = DateTime.UtcNow.AddDays(2),
        AppointmentEndTime = DateTime.UtcNow.AddDays(2).AddHours(1),
        UserId = Guid.NewGuid(),
        UserName = "Provider One",
        VisitAddressLine1 = "123 Main",
        VisitCity = "Columbus",
        VisitState = "OH",
        VisitPostalCode = "43004"
    };

    private static Mock<IAppointmentTypeRepository> CreateAppointmentTypeRepository()
    {
        var appointmentTypeId = Guid.Parse("40f8ca77-e5a8-45ef-b2b0-0e8f515d3d74");
        var repository = new Mock<IAppointmentTypeRepository>();
        repository
            .Setup(x => x.GetByIdAsync(
                It.IsAny<Guid>(),
                It.Is<Guid>(id => id == appointmentTypeId),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync((Guid tenantId, Guid id, CancellationToken _, bool __) => new AppointmentTypeDefinition
            {
                Id = id,
                TenantId = tenantId,
                Name = "Routine Checkup",
                IsActive = true
            });

        return repository;
    }
}
