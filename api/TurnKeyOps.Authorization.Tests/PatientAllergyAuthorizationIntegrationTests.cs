using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MedInsights.Authorization.Tests.Infrastructure;
using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Authorization.Tests;

public sealed class PatientAllergyAuthorizationIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public PatientAllergyAuthorizationIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MemberCanReadPatientAllergies()
    {
        var patientId = Guid.NewGuid();
        _factory.SeedAllergy(new PatientAllergy
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            PartitionKey = EntityKeyPolicy.TenantPatientPartition(_factory.TenantId, patientId),
            RowKey = Guid.NewGuid().ToString("D"),
            AllergyType = "Food",
            Description = "Peanuts",
            DateNoted = DateTime.UtcNow
        });

        using var client = _factory.CreateClientForRole(TenantRoleCatalog.Member);
        using var response = await client.GetAsync($"/api/patients/{patientId}/allergies");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
    }

    [Fact]
    public async Task MemberCannotCreatePatientAllergy()
    {
        using var client = _factory.CreateClientForRole(TenantRoleCatalog.Member);
        using var response = await client.PostAsJsonAsync("/api/PatientAllergies", CreateDto());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BillingAdminCannotDeletePatientAllergy()
    {
        var dto = CreateDto();
        _factory.SeedAllergy(new PatientAllergy
        {
            Id = dto.Id,
            PatientId = dto.PatientId,
            PartitionKey = EntityKeyPolicy.TenantPatientPartition(_factory.TenantId, dto.PatientId),
            RowKey = EntityKeyPolicy.Row(dto.Id),
            AllergyType = dto.AllergyType,
            Description = dto.Description,
            Severity = dto.Severity,
            Reaction = dto.Reaction,
            DateNoted = dto.DateNoted
        });

        using var client = _factory.CreateClientForRole(TenantRoleCatalog.BillingAdmin);
        using var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/PatientAllergies")
        {
            Content = JsonContent.Create(dto)
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminTokenFlowsThroughControllerServiceAndRepository()
    {
        var patientId = Guid.NewGuid();
        var dto = CreateDto(patientId, severity: "Severe");

        using var client = _factory.CreateClientForRole(TenantRoleCatalog.Admin);
        using var createResponse = await client.PostAsJsonAsync("/api/PatientAllergies", dto);
        var createBody = await createResponse.Content.ReadAsStringAsync();

        Assert.True(createResponse.StatusCode == HttpStatusCode.OK, createBody);

        using var getResponse = await client.GetAsync($"/api/patients/{patientId}/allergies");
        var getBody = await getResponse.Content.ReadAsStringAsync();
        Assert.True(getResponse.StatusCode == HttpStatusCode.OK, getBody);

        var payload = JsonSerializer.Deserialize<ApiResponse<List<PatientAllergyDto>>>(getBody, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(payload);
        Assert.True(payload!.Success);
        Assert.Contains(payload.Data ?? [], x => x.Id == dto.Id && x.Severity == "Severe");
    }

    private static PatientAllergyDto CreateDto(Guid? patientId = null, string? severity = null) => new()
    {
        Id = Guid.NewGuid(),
        PatientId = patientId ?? Guid.NewGuid(),
        AllergyType = "Medication",
        Severity = severity,
        Description = "Penicillin",
        Reaction = "Rash",
        DateNoted = DateTime.UtcNow
    };
}
