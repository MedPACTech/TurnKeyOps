using MedInsights.Lib.Dtos;
using MedInsights.Lib.Enums;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;
using Microsoft.VisualBasic.FileIO;
using System.Globalization;
using System.Text;

namespace MedInsights.Services
{

    public class PatientService : IPatientService
    {
        private static readonly HashSet<PatientRelationship> AllowedMinorPrimaryRelationships =
        [
            PatientRelationship.Mother,
            PatientRelationship.Father,
            PatientRelationship.Relative,
            PatientRelationship.Guardian
        ];

        private static readonly IReadOnlyDictionary<string, Func<PatientDto, string>> ExportFieldSelectors =
            new Dictionary<string, Func<PatientDto, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = patient => patient.Id.ToString(),
                ["patientId"] = patient => patient.PatientId.ToString(),
                ["firstName"] = patient => patient.FirstName,
                ["lastName"] = patient => patient.LastName,
                ["fullName"] = patient => $"{patient.FirstName} {patient.LastName}".Trim(),
                ["dateOfBirth"] = patient => patient.DateOfBirth.ToString("yyyy-MM-dd"),
                ["gender"] = patient => patient.Gender,
                ["patientStatus"] = patient => patient.PatientStatus,
                ["currentFacilityId"] = patient => patient.CurrentFacilityId?.ToString() ?? string.Empty,
                ["currentFacilityName"] = patient => patient.CurrentFacilityName ?? string.Empty,
                ["currentFacilityAdmitDate"] = patient => patient.CurrentFacilityAdmitDate?.ToString("O") ?? string.Empty,
                ["currentFacilityStatus"] = patient => patient.CurrentFacilityStatus ?? string.Empty,
                ["dateCreated"] = patient => patient.DateCreated?.ToString("O") ?? string.Empty,
                ["dateUpdated"] = patient => patient.DateUpdated?.ToString("O") ?? string.Empty
            };

        private readonly IPatientRepository _patientRepository;
        private readonly IUserContext _userContext;
        private readonly IPatientContextService _patientContextService;
        private readonly IPatientContactService _patientContactService;
        private readonly IFacilityService _facilityService;
        private readonly ITenantMembershipAuthorizationService _tenantMembershipAuthorizationService;

        public PatientService(
            IPatientRepository patientRepository,
            IUserContext userContext,
            IPatientContextService patientContextService,
            IPatientContactService patientContactService,
            IFacilityService facilityService,
            ITenantMembershipAuthorizationService tenantMembershipAuthorizationService)
        {
            _patientRepository = patientRepository;
            _userContext = userContext;
            _patientContextService = patientContextService;
            _patientContactService = patientContactService;
            _facilityService = facilityService;
            _tenantMembershipAuthorizationService = tenantMembershipAuthorizationService;
        }

        private string PartitionKeyforTenant() => EntityKeyPolicy.TenantPartition(_userContext.TenantId);

        // Read single by Id (resolves tenant/user from claims)
        public async Task<PatientDto?> GetAsync(Guid Id)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();
            var pk = PartitionKeyforTenant();
            var rowKey = EntityKeyPolicy.Row(Id);
            var patient = await _patientRepository.GetAsync(pk, rowKey);

            if (patient == null) return null;

            return patient == null ? null : PatientMapper.ToDto(patient);
        }

        // Read all for current user
        public async Task<(IEnumerable<PatientDto> Patients, string? ContinuationToken)>
            GetPagedAsync(int pageSize, string? continuationToken = null)
            {
                if (!_userContext.IsAuthenticated)
                    throw new UnauthorizedAccessException();

                var pk = PartitionKeyforTenant();
                var (patients, nextToken) = await _patientRepository
                    .GetByPartitionPagedAsync(pk, pageSize, continuationToken);

                return (patients.Select(PatientMapper.ToDto), nextToken);
            }


        // Create
        public async Task<PatientDto> AddAsync(PatientDto patientDto)
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            var normalizedPatient = PrepareForCreate(patientDto, DateTime.UtcNow);
            var isMinor = IsUnder18(normalizedPatient.DateOfBirth, DateOnly.FromDateTime(DateTime.UtcNow));
            if (isMinor)
            {
                ValidateMinorPrimaryContactPayload(normalizedPatient);
            }

            var requestedFacilityId = normalizedPatient.CurrentFacilityId;
            if (requestedFacilityId.HasValue && requestedFacilityId.Value != Guid.Empty)
            {
                var facility = await _facilityService.GetAsync(requestedFacilityId.Value);
                if (facility is null)
                    throw new KeyNotFoundException("Facility not found.");
            }

            normalizedPatient.CurrentFacilityId = null;
            normalizedPatient.CurrentFacilityName = null;
            normalizedPatient.CurrentFacilityAdmitDate = null;
            normalizedPatient.CurrentFacilityStatus = null;
            var created = await SaveNewPatientAsync(normalizedPatient);
            await EnsureSelfContactAsync(created, normalizedPatient, includeCommunication: !isMinor);

            if (isMinor)
            {
                await AddMinorPrimaryContactAsync(created.Id, normalizedPatient);
            }

            if (requestedFacilityId.HasValue && requestedFacilityId.Value != Guid.Empty)
            {
                await _facilityService.AdmitPatientAsync(
                    requestedFacilityId.Value,
                    new AdmitFacilityPatientDto { PatientId = created.Id });

                created = await GetAsync(created.Id) ?? created;
            }

            return created;
        }

        public async Task<BulkPatientUploadResultDto> BulkUploadAsync(Stream csvStream, CancellationToken ct = default)
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            ArgumentNullException.ThrowIfNull(csvStream);

            using var parser = new TextFieldParser(csvStream)
            {
                TextFieldType = FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = true
            };
            parser.SetDelimiters(",");

            if (parser.EndOfData)
            {
                throw new ArgumentException("The uploaded CSV file is empty.", nameof(csvStream));
            }

            var headers = parser.ReadFields();
            if (headers == null || headers.Length == 0)
            {
                throw new ArgumentException("The uploaded CSV file must include a header row.", nameof(csvStream));
            }

            var headerIndex = BuildHeaderIndex(headers);
            EnsureRequiredHeaders(headerIndex);

            var result = new BulkPatientUploadResultDto();
            var nowUtc = DateTime.UtcNow;
            var currentRowNumber = 1;

            while (!parser.EndOfData)
            {
                ct.ThrowIfCancellationRequested();
                currentRowNumber++;

                string[]? fields;
                try
                {
                    fields = parser.ReadFields();
                }
                catch (MalformedLineException ex)
                {
                    result.TotalRows++;
                    result.FailedCount++;
                    result.Rows.Add(new BulkPatientUploadRowResultDto
                    {
                        RowNumber = currentRowNumber,
                        Success = false,
                        Error = $"Malformed CSV row: {ex.Message}"
                    });
                    continue;
                }

                if (fields == null || RowIsBlank(fields))
                {
                    continue;
                }

                result.TotalRows++;

                try
                {
                    var patient = ParsePatient(fields, headerIndex, currentRowNumber);
                    var normalizedPatient = PrepareForCreate(patient, nowUtc);
                    var createdPatient = await SaveNewPatientAsync(normalizedPatient);
                    await EnsureSelfContactAsync(createdPatient, normalizedPatient, includeCommunication: false);

                    result.CreatedCount++;
                    result.Rows.Add(new BulkPatientUploadRowResultDto
                    {
                        RowNumber = currentRowNumber,
                        Success = true,
                        PatientId = createdPatient.Id,
                        FirstName = createdPatient.FirstName,
                        LastName = createdPatient.LastName
                    });
                }
                catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is ApplicationException)
                {
                    result.FailedCount++;
                    result.Rows.Add(new BulkPatientUploadRowResultDto
                    {
                        RowNumber = currentRowNumber,
                        Success = false,
                        FirstName = TryGetField(fields, headerIndex, "firstname"),
                        LastName = TryGetField(fields, headerIndex, "lastname"),
                        Error = ex.Message
                    });
                }
            }

            return result;
        }


        // Search (by first name, last name, or both)
        // In PatientService
        //TODO: patient search filter may be added here later to help with advanced search
        public async Task<List<PatientDto>> SearchAsync(string terms)
        {
            if (string.IsNullOrWhiteSpace(terms))
            {
                throw new ArgumentException("At least one search term is required.", nameof(terms));
            }

            // Split by comma, trim whitespace, and remove empties
            var splitTerms = terms
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToArray();

            if (splitTerms.Length == 0)
            {
                throw new ArgumentException("At least one valid search term is required.", nameof(terms));
            }

            var pk = PartitionKeyforTenant();
            var patients = await _patientRepository.SearchPatientAsync(pk, splitTerms);
            return patients.Select(PatientMapper.ToDto).ToList();
        }

        // Historical Patients
        public async Task<List<PatientDto>> GetHistoricalPatientsAsync(int scope)
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            var historicalPatientsContext = await _patientContextService.GetHistoryAsync();

            var historicalPatients = historicalPatientsContext
            .Select(ctx => new PatientDto
            {
                Id = ctx.PatientId,
                FirstName = ctx.FirstName,
                LastName = ctx.LastName,
                Gender = ctx.Gender,
                PatientStatus = "Active",
                DateOfBirth = DateOnly.FromDateTime(ctx.DateOfBirth.ToDateTime(TimeOnly.MinValue)),
                PatientId = ctx.PatientId
            })
            .Take(scope);

            return historicalPatients.ToList();
        }


        // Update (guard against cross-tenant updates)
        public async Task<PatientDto> UpdateAsync(PatientDto patient)
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            //convert patientDto to patient entity
            var existingPatient = await _patientRepository.GetAsync(PartitionKeyforTenant(), patient.Id.ToString("D"))
                                ?? throw new KeyNotFoundException("Patient not found.");

            var expectedPk = PartitionKeyforTenant();
            if (!string.Equals(existingPatient.PartitionKey, expectedPk, StringComparison.Ordinal))
                throw new UnauthorizedAccessException("Cannot modify a patient outside your tenant/user.");

            existingPatient.FirstName = patient.FirstName;
            existingPatient.LastName = patient.LastName;
            existingPatient.DateOfBirth = DateTime.SpecifyKind(patient.DateOfBirth.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            existingPatient.Gender = patient.Gender;
            existingPatient.PatientStatus = NormalizePatientStatus(patient.PatientStatus);
            existingPatient.PhysicalAddressLine1 = Normalize(patient.PhysicalAddressLine1);
            existingPatient.PhysicalAddressLine2 = Normalize(patient.PhysicalAddressLine2);
            existingPatient.PhysicalCity = Normalize(patient.PhysicalCity);
            existingPatient.PhysicalState = Normalize(patient.PhysicalState);
            existingPatient.PhysicalPostalCode = Normalize(patient.PhysicalPostalCode);
            existingPatient.PhysicalCountry = Normalize(patient.PhysicalCountry);
            existingPatient.MailingAddressLine1 = Normalize(patient.MailingAddressLine1);
            existingPatient.MailingAddressLine2 = Normalize(patient.MailingAddressLine2);
            existingPatient.MailingCity = Normalize(patient.MailingCity);
            existingPatient.MailingState = Normalize(patient.MailingState);
            existingPatient.MailingPostalCode = Normalize(patient.MailingPostalCode);
            existingPatient.MailingCountry = Normalize(patient.MailingCountry);
            existingPatient.BillingAddressLine1 = Normalize(patient.BillingAddressLine1);
            existingPatient.BillingAddressLine2 = Normalize(patient.BillingAddressLine2);
            existingPatient.BillingCity = Normalize(patient.BillingCity);
            existingPatient.BillingState = Normalize(patient.BillingState);
            existingPatient.BillingPostalCode = Normalize(patient.BillingPostalCode);
            existingPatient.BillingCountry = Normalize(patient.BillingCountry);
            existingPatient.DateUpdated = DateTime.UtcNow;

            await _patientRepository.SaveAsync(existingPatient);

            return PatientMapper.ToDto(existingPatient);
        }


        // Soft Delete
        public async Task DeleteAsync(Guid id)
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            var pk = PartitionKeyforTenant();
            var rk = EntityKeyPolicy.Row(id);

            var patient = await _patientRepository.GetAsync(pk, rk)
                        ?? throw new KeyNotFoundException("Patient not found.");

            // Apply soft delete
            patient.IsDeleted = true;
            patient.DateUpdated = DateTime.UtcNow;

            await _patientRepository.SaveAsync(patient, CancellationToken.None);
        }


        public async Task<Dictionary<Guid, PatientDto>> GetByIdsAsync(IEnumerable<Guid> ids)
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            if (ids == null)
                throw new ArgumentNullException(nameof(ids));

            // de-dupe + ignore empties
            var distinctIds = ids
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            var results = new Dictionary<Guid, PatientDto>();

            if (distinctIds.Count == 0)
                return results;

            // throttle to avoid spiking Azure Table requests
            const int maxConcurrency = 12;
            using var throttler = new SemaphoreSlim(maxConcurrency);

            var pk = PartitionKeyforTenant();

            var tasks = distinctIds.Select(async id =>
            {
                await throttler.WaitAsync();
                try
                {
                    var rk = EntityKeyPolicy.Row(id);
                    var patient = await _patientRepository.GetAsync(pk, rk);

                    if (patient != null && patient.IsDeleted != true)
                    {
                        lock (results)
                        {
                            results[id] = PatientMapper.ToDto(patient);
                        }
                    }
                }
                finally
                {
                    throttler.Release();
                }
            });

            await Task.WhenAll(tasks);

            return results;
        }

        public async Task<PatientDto> ActivateAsync(Guid patientId)
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            var patient = await GetAsync(patientId)
                            ?? throw new KeyNotFoundException("Patient not found.");

            await _patientContextService.ActivateAsync(patient);
            return patient;
        }

        public async Task<PatientDto?> GetActiveAsync()
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            var historicalPatientContext = await _patientContextService.GetActiveAsync();

            if (historicalPatientContext == null)
            {
                return null;
            }
            
            var historicalPatient = new PatientDto
            {
                Id = historicalPatientContext.PatientId,
                FirstName = historicalPatientContext.FirstName,
                LastName = historicalPatientContext.LastName,
                Gender = historicalPatientContext.Gender,
                PatientStatus = "Active",
                DateOfBirth = DateOnly.FromDateTime(historicalPatientContext.DateOfBirth.ToDateTime(TimeOnly.MinValue))
            };
           
            return historicalPatient;
        }

        public async Task<(byte[] Content, string FileName)> ExportAsync(PatientExportRequestDto request, CancellationToken ct = default)
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            ArgumentNullException.ThrowIfNull(request);
            await _tenantMembershipAuthorizationService.RequireMembershipManagementAccessAsync(ct);

            var fields = NormalizeExportFields(request.Fields);
            var patients = await _patientRepository.GetByPartitionAsync(PartitionKeyforTenant(), ct);
            var patientDtos = patients.Select(PatientMapper.ToDto);

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var search = request.Search.Trim();
                patientDtos = patientDtos.Where(patient =>
                    patient.FirstName.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || patient.LastName.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || $"{patient.FirstName} {patient.LastName}".Contains(search, StringComparison.OrdinalIgnoreCase)
                    || patient.PatientId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            var orderedPatients = patientDtos
                .OrderBy(patient => patient.LastName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(patient => patient.FirstName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var csv = BuildCsv(fields, orderedPatients);
            var fileName = $"patients-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
            return (Encoding.UTF8.GetBytes(csv), fileName);
        }

        private static string NormalizePatientStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return "Active";

            return status.Trim().ToLowerInvariant() switch
            {
                "active" => "Active",
                "inactive" => "Inactive",
                _ => throw new ArgumentException("Patient status must be 'Active' or 'Inactive'.", nameof(status))
            };
        }

        private static List<string> NormalizeExportFields(IEnumerable<string>? fields)
        {
            var normalized = (fields ?? Array.Empty<string>())
                .Where(field => !string.IsNullOrWhiteSpace(field))
                .Select(field => field.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalized.Count == 0)
                throw new ArgumentException("At least one export field is required.", nameof(fields));

            var invalid = normalized
                .Where(field => !ExportFieldSelectors.ContainsKey(field))
                .OrderBy(field => field, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (invalid.Count > 0)
                throw new ArgumentException($"Unsupported export fields: {string.Join(", ", invalid)}", nameof(fields));

            return normalized;
        }

        private static string BuildCsv(IReadOnlyList<string> fields, IReadOnlyList<PatientDto> patients)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", fields.Select(EscapeCsv)));

            foreach (var patient in patients)
            {
                var row = fields.Select(field => EscapeCsv(ExportFieldSelectors[field](patient)));
                sb.AppendLine(string.Join(",", row));
            }

            return sb.ToString();
        }

        private static string EscapeCsv(string? value)
        {
            var text = value ?? string.Empty;
            if (text.Contains('"'))
                text = text.Replace("\"", "\"\"");

            if (text.IndexOfAny([',', '"', '\r', '\n']) >= 0)
                return $"\"{text}\"";

            return text;
        }

        private async Task<PatientDto> SaveNewPatientAsync(PatientDto patientDto)
        {
            var entity = PatientMapper.ToEntity(patientDto, PartitionKeyforTenant());

            try
            {
                await _patientRepository.SaveAsync(entity);
            }
            catch (Exception ex)
            {
                throw new ApplicationException("An error occurred while adding the patient.", ex);
            }

            return PatientMapper.ToDto(entity);
        }

        private static PatientDto PrepareForCreate(PatientDto patientDto, DateTime nowUtc)
        {
            if (patientDto.Id == Guid.Empty)
            {
                patientDto.Id = Guid.NewGuid();
            }

            patientDto.FirstName = patientDto.FirstName?.Trim() ?? string.Empty;
            patientDto.LastName = patientDto.LastName?.Trim() ?? string.Empty;
            patientDto.Gender = patientDto.Gender?.Trim() ?? string.Empty;
            patientDto.Phone = Normalize(patientDto.Phone);
            patientDto.Email = Normalize(patientDto.Email);
            patientDto.PrimaryFirstName = Normalize(patientDto.PrimaryFirstName);
            patientDto.PrimaryLastName = Normalize(patientDto.PrimaryLastName);
            patientDto.PhysicalAddressLine1 = Normalize(patientDto.PhysicalAddressLine1);
            patientDto.PhysicalAddressLine2 = Normalize(patientDto.PhysicalAddressLine2);
            patientDto.PhysicalCity = Normalize(patientDto.PhysicalCity);
            patientDto.PhysicalState = Normalize(patientDto.PhysicalState);
            patientDto.PhysicalPostalCode = Normalize(patientDto.PhysicalPostalCode);
            patientDto.PhysicalCountry = Normalize(patientDto.PhysicalCountry);
            patientDto.MailingAddressLine1 = Normalize(patientDto.MailingAddressLine1);
            patientDto.MailingAddressLine2 = Normalize(patientDto.MailingAddressLine2);
            patientDto.MailingCity = Normalize(patientDto.MailingCity);
            patientDto.MailingState = Normalize(patientDto.MailingState);
            patientDto.MailingPostalCode = Normalize(patientDto.MailingPostalCode);
            patientDto.MailingCountry = Normalize(patientDto.MailingCountry);
            patientDto.BillingAddressLine1 = Normalize(patientDto.BillingAddressLine1);
            patientDto.BillingAddressLine2 = Normalize(patientDto.BillingAddressLine2);
            patientDto.BillingCity = Normalize(patientDto.BillingCity);
            patientDto.BillingState = Normalize(patientDto.BillingState);
            patientDto.BillingPostalCode = Normalize(patientDto.BillingPostalCode);
            patientDto.BillingCountry = Normalize(patientDto.BillingCountry);

            // TODO: add MRN and PatientId generation logic later
            patientDto.PatientStatus = NormalizePatientStatus(patientDto.PatientStatus);
            patientDto.DateUpdated = nowUtc;
            patientDto.DateCreated = nowUtc;

            return patientDto;
        }

        private async Task EnsureSelfContactAsync(PatientDto patient, PatientDto payload, bool includeCommunication)
        {
            var existingContacts = await _patientContactService.GetByPatientAsync(patient.Id);
            if (existingContacts.Any(c => c.Relationship == PatientRelationship.Self))
                return;

            await _patientContactService.AddAsync(new PatientContactDto
            {
                Id = Guid.NewGuid(),
                PatientId = patient.Id,
                ContactType = ContactType.Self,
                Relationship = PatientRelationship.Self,
                IsPrimary = true,
                IsSecondary = false,
                FirstName = patient.FirstName,
                LastName = patient.LastName,
                PrimaryPhone = includeCommunication ? payload.Phone : null,
                Email = includeCommunication ? payload.Email : null,
                HasHIPAAPermission = includeCommunication && payload.HasHIPAAPermission,
                HasBillingPermission = includeCommunication && payload.HasBillingPermission
            });
        }

        private async Task AddMinorPrimaryContactAsync(Guid patientId, PatientDto payload)
        {
            var relationship = payload.Relationship!.Value;
            var contactType = relationship == PatientRelationship.Guardian
                ? ContactType.LegalGuardian
                : ContactType.Emergency;

            await _patientContactService.AddAsync(new PatientContactDto
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                ContactType = contactType,
                Relationship = relationship,
                IsPrimary = true,
                IsSecondary = false,
                FirstName = payload.PrimaryFirstName!,
                LastName = payload.PrimaryLastName!,
                PrimaryPhone = payload.Phone,
                Email = payload.Email,
                HasHIPAAPermission = payload.HasHIPAAPermission,
                HasBillingPermission = payload.HasBillingPermission
            });
        }

        private static void ValidateMinorPrimaryContactPayload(PatientDto patient)
        {
            if (string.IsNullOrWhiteSpace(patient.PrimaryFirstName))
                throw new ArgumentException("PrimaryFirstName is required for patients under 18.", nameof(patient));

            if (string.IsNullOrWhiteSpace(patient.PrimaryLastName))
                throw new ArgumentException("PrimaryLastName is required for patients under 18.", nameof(patient));

            if (!patient.Relationship.HasValue)
                throw new ArgumentException("Relationship is required for patients under 18.", nameof(patient));

            if (!AllowedMinorPrimaryRelationships.Contains(patient.Relationship.Value))
                throw new ArgumentException("Relationship must be Mother, Father, Relative, or Guardian for patients under 18.", nameof(patient));
        }

        private static bool IsUnder18(DateOnly dateOfBirth, DateOnly today)
        {
            var age = today.Year - dateOfBirth.Year;
            if (dateOfBirth > today.AddYears(-age))
                age--;

            return age < 18;
        }

        private static string? Normalize(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static Dictionary<string, int> BuildHeaderIndex(string[] headers)
        {
            var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < headers.Length; i++)
            {
                var normalizedHeader = NormalizeHeader(headers[i]);
                if (!string.IsNullOrWhiteSpace(normalizedHeader) && !index.ContainsKey(normalizedHeader))
                {
                    index[normalizedHeader] = i;
                }
            }

            return index;
        }

        private static void EnsureRequiredHeaders(IReadOnlyDictionary<string, int> headerIndex)
        {
            var requiredHeaders = new[] { "firstname", "lastname", "dateofbirth", "gender" };
            var missingHeaders = requiredHeaders.Where(header => !headerIndex.ContainsKey(header)).ToList();

            if (missingHeaders.Count > 0)
            {
                throw new ArgumentException($"The uploaded CSV file is missing required headers: {string.Join(", ", missingHeaders)}.");
            }
        }

        private static PatientDto ParsePatient(string[] fields, IReadOnlyDictionary<string, int> headerIndex, int rowNumber)
        {
            var firstName = RequireField(fields, headerIndex, "firstname", rowNumber);
            var lastName = RequireField(fields, headerIndex, "lastname", rowNumber);
            var gender = RequireField(fields, headerIndex, "gender", rowNumber);
            var dateOfBirthValue = RequireField(fields, headerIndex, "dateofbirth", rowNumber);

            if (!TryParseDateOfBirth(dateOfBirthValue, out var dateOfBirth))
            {
                throw new FormatException($"Row {rowNumber}: dateOfBirth must be a valid date.");
            }

            if (dateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
            {
                throw new ArgumentException($"Row {rowNumber}: dateOfBirth cannot be in the future.");
            }

            return new PatientDto
            {
                FirstName = firstName,
                LastName = lastName,
                DateOfBirth = dateOfBirth,
                Gender = gender,
                PatientStatus = GetOptionalField(fields, headerIndex, "patientstatus") ?? "Active"
            };
        }

        private static bool TryParseDateOfBirth(string value, out DateOnly dateOfBirth)
        {
            return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateOfBirth)
                || DateOnly.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out dateOfBirth);
        }

        private static string RequireField(string[] fields, IReadOnlyDictionary<string, int> headerIndex, string fieldName, int rowNumber)
        {
            var value = GetOptionalField(fields, headerIndex, fieldName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            throw new ArgumentException($"Row {rowNumber}: {ToDisplayFieldName(fieldName)} is required.");
        }

        private static string? GetOptionalField(string[] fields, IReadOnlyDictionary<string, int> headerIndex, string fieldName)
        {
            if (!headerIndex.TryGetValue(fieldName, out var index) || index < 0 || index >= fields.Length)
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(fields[index]) ? null : fields[index].Trim();
        }

        private static string? TryGetField(string[] fields, IReadOnlyDictionary<string, int> headerIndex, string fieldName)
        {
            return GetOptionalField(fields, headerIndex, fieldName);
        }

        private static string NormalizeHeader(string header)
        {
            var normalized = new string(header
                .Where(c => char.IsLetterOrDigit(c))
                .ToArray())
                .ToLowerInvariant();

            return normalized switch
            {
                "dob" => "dateofbirth",
                "status" => "patientstatus",
                _ => normalized
            };
        }

        private static bool RowIsBlank(IEnumerable<string> fields)
        {
            return fields.All(string.IsNullOrWhiteSpace);
        }

        private static string ToDisplayFieldName(string fieldName)
        {
            return fieldName switch
            {
                "firstname" => "firstName",
                "lastname" => "lastName",
                "dateofbirth" => "dateOfBirth",
                "patientstatus" => "patientStatus",
                _ => fieldName
            };
        }
    }

}


