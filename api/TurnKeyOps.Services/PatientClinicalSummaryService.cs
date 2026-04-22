using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MedInsights.Lib;
using MedInsights.Lib.Configurations;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using Microsoft.Extensions.Options;
using OpenAIChatMessage = OpenAI.Chat.ChatMessage;

namespace MedInsights.Services
{
    public sealed class PatientClinicalSummaryService : IPatientClinicalSummaryService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IAIService<OpenAIChatMessage> _aiService;
        private readonly OpenAISettings _openAiSettings;
        private readonly PatientClinicalSummarySettings _settings;
        private readonly IUserContext _userContext;
        private readonly IPatientService _patientService;
        private readonly IPatientContactService _patientContactService;
        private readonly IPatientAllergyService _patientAllergyService;
        private readonly IPatientMedicationService _patientMedicationService;
        private readonly IPatientLabsService _patientLabsService;
        private readonly IPatientVitalsService _patientVitalsService;
        private readonly IPatientInsuranceService _patientInsuranceService;
        private readonly IPatientOrderService _patientOrderService;
        private readonly IPatientNoteService _patientNoteService;
        private readonly IPatientReferralRepository _patientReferralRepository;
        private readonly IPatientBillingNoteService _patientBillingNoteService;
        private readonly IPatientAppointmentService _patientAppointmentService;
        private readonly IPatientEncounterRepository _patientEncounterRepository;
        private readonly IPatientClinicalSummaryCacheRepository _patientClinicalSummaryCacheRepository;

        public PatientClinicalSummaryService(
            IAIService<OpenAIChatMessage> aiService,
            IOptions<OpenAISettings> openAiSettings,
            IOptions<PatientClinicalSummarySettings> settings,
            IUserContext userContext,
            IPatientService patientService,
            IPatientContactService patientContactService,
            IPatientAllergyService patientAllergyService,
            IPatientMedicationService patientMedicationService,
            IPatientLabsService patientLabsService,
            IPatientVitalsService patientVitalsService,
            IPatientInsuranceService patientInsuranceService,
            IPatientOrderService patientOrderService,
            IPatientNoteService patientNoteService,
            IPatientReferralRepository patientReferralRepository,
            IPatientBillingNoteService patientBillingNoteService,
            IPatientAppointmentService patientAppointmentService,
            IPatientEncounterRepository patientEncounterRepository,
            IPatientClinicalSummaryCacheRepository patientClinicalSummaryCacheRepository)
        {
            _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
            _openAiSettings = openAiSettings?.Value ?? throw new ArgumentNullException(nameof(openAiSettings));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
            _patientService = patientService ?? throw new ArgumentNullException(nameof(patientService));
            _patientContactService = patientContactService ?? throw new ArgumentNullException(nameof(patientContactService));
            _patientAllergyService = patientAllergyService ?? throw new ArgumentNullException(nameof(patientAllergyService));
            _patientMedicationService = patientMedicationService ?? throw new ArgumentNullException(nameof(patientMedicationService));
            _patientLabsService = patientLabsService ?? throw new ArgumentNullException(nameof(patientLabsService));
            _patientVitalsService = patientVitalsService ?? throw new ArgumentNullException(nameof(patientVitalsService));
            _patientInsuranceService = patientInsuranceService ?? throw new ArgumentNullException(nameof(patientInsuranceService));
            _patientOrderService = patientOrderService ?? throw new ArgumentNullException(nameof(patientOrderService));
            _patientNoteService = patientNoteService ?? throw new ArgumentNullException(nameof(patientNoteService));
            _patientReferralRepository = patientReferralRepository ?? throw new ArgumentNullException(nameof(patientReferralRepository));
            _patientBillingNoteService = patientBillingNoteService ?? throw new ArgumentNullException(nameof(patientBillingNoteService));
            _patientAppointmentService = patientAppointmentService ?? throw new ArgumentNullException(nameof(patientAppointmentService));
            _patientEncounterRepository = patientEncounterRepository ?? throw new ArgumentNullException(nameof(patientEncounterRepository));
            _patientClinicalSummaryCacheRepository = patientClinicalSummaryCacheRepository ?? throw new ArgumentNullException(nameof(patientClinicalSummaryCacheRepository));
        }

        public async Task<PatientClinicalSummaryDto> GenerateAsync(Guid patientId, bool forceRefresh = false, CancellationToken ct = default)
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            if (patientId == Guid.Empty)
                throw new ArgumentException("patientId is required.", nameof(patientId));

            var patient = await _patientService.GetAsync(patientId)
                ?? throw new KeyNotFoundException("Patient not found.");

            var generatedAt = DateTime.UtcNow;
            var maxItems = Math.Max(_settings.MaxItemsPerSection, 1);
            var issues = new ConcurrentBag<string>();
            var patientPartition = EntityKeyPolicy.TenantPatientPartition(_userContext.TenantId, patientId);
            var cachedSummary = forceRefresh
                ? null
                : await _patientClinicalSummaryCacheRepository.GetAsync(_userContext.TenantId, patientId, ct);

            var contactsTask = SafeFetchAsync("patient contacts", () => _patientContactService.GetByPatientAsync(patientId), Enumerable.Empty<PatientContactDto>(), issues);
            var allergiesTask = SafeFetchAsync("allergies", () => _patientAllergyService.GetByPatientAsync(patientId), Enumerable.Empty<PatientAllergyDto>(), issues);
            var medicationsTask = SafeFetchAsync("medications", () => _patientMedicationService.GetByPatientAsync(patientId), Enumerable.Empty<PatientMedicationDto>(), issues);
            var labsTask = SafeFetchAsync("labs", () => _patientLabsService.GetByPatientAsync(patientId), Enumerable.Empty<PatientLabsDto>(), issues);
            var vitalsTask = SafeFetchAsync("vitals", () => _patientVitalsService.GetByPatientAsync(patientId), Enumerable.Empty<PatientVitalsDto>(), issues);
            var insuranceTask = SafeFetchAsync("insurance", () => _patientInsuranceService.GetByPatientAsync(patientId), Enumerable.Empty<PatientInsuranceDto>(), issues);
            var ordersTask = SafeFetchAsync("orders", () => _patientOrderService.GetByPatientAsync(patientId), Enumerable.Empty<PatientOrderDto>(), issues);
            var notesTask = SafeFetchAsync("notes", () => _patientNoteService.GetByPatientIdAsync(patientId), Enumerable.Empty<PatientNoteDto>(), issues);
            var referralsTask = SafeFetchAsync(
                "referrals",
                () => _patientReferralRepository.GetByPatientAsync(patientPartition),
                (IReadOnlyList<PatientReferral>)Array.Empty<PatientReferral>(),
                issues);
            var billingNotesTask = SafeFetchAsync("billing notes", () => _patientBillingNoteService.GetByPatientAsync(patientId), Enumerable.Empty<PatientBillingNoteDto>(), issues);
            var encountersTask = SafeFetchAsync("encounters", () => _patientEncounterRepository.GetByPartitionAsync(patientPartition, ct), (IReadOnlyList<PatientEncounter>)Array.Empty<PatientEncounter>(), issues);
            var appointmentsTask = SafeFetchAsync(
                "appointments",
                () => _patientAppointmentService.SearchAsync(new PatientAppointmentService.AppointmentSearch
                {
                    PatientId = patientId,
                    Page = 1,
                    PageSize = Math.Max(50, maxItems * 2),
                    Sort = "start",
                    Order = "desc"
                }, ct),
                Enumerable.Empty<PatientAppointmentDto>(),
                issues);

            await Task.WhenAll(
                contactsTask,
                allergiesTask,
                medicationsTask,
                labsTask,
                vitalsTask,
                insuranceTask,
                ordersTask,
                notesTask,
                referralsTask,
                billingNotesTask,
                encountersTask,
                appointmentsTask);

            var contacts = contactsTask.Result.OrderByDescending(x => x.IsPrimary).Take(maxItems).Select(x => new
            {
                x.ContactType,
                x.Relationship,
                x.IsPrimary,
                x.FirstName,
                x.LastName,
                x.PrimaryPhone,
                x.Email,
                x.HasHIPAAPermission
            });

            var allergies = allergiesTask.Result.OrderByDescending(x => x.DateNoted).Take(maxItems).Select(x => new
            {
                x.AllergyType,
                x.Description,
                x.Reaction,
                x.Severity,
                x.DateNoted
            }).ToList();

            var medications = medicationsTask.Result.OrderByDescending(x => x.DateNoted).Take(maxItems).Select(x => new
            {
                x.Medication,
                x.Strength,
                x.Route,
                x.Frequency,
                x.IsEnded,
                x.DateNoted
            }).ToList();

            var labs = labsTask.Result.OrderByDescending(x => x.DateLabCompleted).Take(maxItems).Select(x => new
            {
                x.LabType,
                x.LabProvider,
                x.DateLabCompleted,
                x.LabStatus
            }).ToList();

            var vitals = vitalsTask.Result.OrderByDescending(x => x.DateRead).Take(maxItems).Select(x => new
            {
                x.DateRead,
                x.SystolicBloodPressure,
                x.DiastolicBloodPressure,
                x.HeartRate,
                x.RespitoryRate,
                x.PulseOximetry,
                x.Weight,
                x.BMI,
                x.Temperature
            }).ToList();

            var insurance = insuranceTask.Result.OrderByDescending(x => x.VerificationDate).Take(maxItems).Select(x => new
            {
                x.Carrier,
                x.Relationship,
                x.EffectiveDate,
                x.VerificationDate
            });

            var orders = ordersTask.Result.OrderByDescending(x => x.DateOrdered).Take(maxItems).Select(x => new
            {
                x.DateOrdered,
                x.OrderingProviderName,
                x.LabType,
                x.LabProvider,
                x.IsComplete
            });

            var notes = notesTask.Result.OrderByDescending(x => x.DateCreated).Take(maxItems).Select(x => new
            {
                x.DateCreated,
                x.Category,
                x.Visibility,
                x.Tags,
                NoteBody = Clip(x.NoteBody, _settings.MaxLongFieldChars)
            }).ToList();

            var referrals = referralsTask.Result.OrderByDescending(x => x.DateUpdated).Take(maxItems).Select(x => new
            {
                x.DateUpdated,
                x.Status,
                x.CaseTitle,
                x.ReferralReason,
                x.Diagnosis,
                CaseSummary = Clip(x.CaseSummary, _settings.MaxLongFieldChars),
                x.NextAction,
                x.NextActionAt
            }).ToList();

            var billingNotes = billingNotesTask.Result.OrderByDescending(x => x.DateSigned).Take(maxItems).Select(x => new
            {
                x.DateSigned,
                x.NoteType,
                x.NoteTitle,
                BillingBody = Clip(x.BillingBody, _settings.MaxLongFieldChars)
            });

            var encounters = encountersTask.Result.OrderByDescending(x => x.DateUpdated).Take(maxItems).Select(x => new
            {
                x.DateUpdated,
                x.NoteType,
                x.NoteTitle,
                x.Status,
                EncounterBody = Clip(x.EncounterBody, _settings.MaxLongFieldChars)
            }).ToList();

            var appointments = appointmentsTask.Result.OrderByDescending(x => x.AppointmentStartTime).Take(Math.Max(maxItems * 2, 12)).Select(x => new
            {
                x.AppointmentStatus,
                x.AppointmentType,
                x.AppointmentLocation,
                x.AppointmentStartTime,
                x.AppointmentEndTime,
                x.Reason
            }).ToList();

            var missingSections = BuildMissingSections(allergies.Count, medications.Count, labs.Count, vitals.Count, encounters.Count, appointments.Count);
            var fallback = BuildFallback(patient, generatedAt, referralsTask.Result.ToList(), encountersTask.Result.ToList(), appointmentsTask.Result.ToList(), missingSections, issues);

            var contextJson = JsonSerializer.Serialize(new
            {
                patient = new
                {
                    patient.Id,
                    patient.PatientId,
                    patient.FirstName,
                    patient.LastName,
                    patient.DateOfBirth,
                    patient.Gender,
                    patient.PatientStatus,
                    Age = CalculateAge(patient.DateOfBirth, generatedAt),
                    patient.CurrentFacilityName
                },
                contacts,
                allergies,
                medications,
                labs,
                vitals,
                insurance,
                orders,
                appointments,
                encounters,
                notes,
                referrals,
                billingNotes,
                dataQuality = new
                {
                    missingSections,
                    retrievalIssues = issues.ToArray()
                }
            }, JsonOptions);
            var dataFingerprint = ComputeFingerprint($"{_settings.PromptVersion}|{contextJson}");

            if (!forceRefresh
                && cachedSummary != null
                && !cachedSummary.IsDeleted
                && string.Equals(cachedSummary.DataFingerprint, dataFingerprint, StringComparison.OrdinalIgnoreCase))
            {
                return ToDto(cachedSummary);
            }

            var model = string.IsNullOrWhiteSpace(_settings.Model) ? _openAiSettings.DefaultModel : _settings.Model;
            if (!string.IsNullOrWhiteSpace(model))
            {
                _aiService.SetServiceModel(model);
            }

            var completion = await _aiService.GetChatCompletionAsync(
                BuildPrompt(),
                [$"Current UTC timestamp: {generatedAt:O}", $"Patient chart context JSON:\n{contextJson}"],
                maxOutputTokens: Math.Max(300, _settings.MaxOutputTokens),
                temperature: _settings.Temperature,
                ct: ct);

            var parsed = ParseModel(completion);
            if (parsed == null)
            {
                await SaveCacheAsync(
                    patientId,
                    generatedAt,
                    dataFingerprint,
                    fallback,
                    cachedSummary,
                    ct);
                return fallback;
            }

            var result = new PatientClinicalSummaryDto
            {
                PatientId = patient.Id,
                Narrative = Clip(FirstNonEmpty(parsed.Narrative, fallback.Narrative), _settings.MaxNarrativeChars),
                ActiveConditions = NormalizeList(parsed.ActiveConditions, fallback.ActiveConditions),
                MostRecentConcern = FirstNonEmpty(parsed.MostRecentConcern, fallback.MostRecentConcern),
                CareGaps = NormalizeList(parsed.CareGaps, fallback.CareGaps),
                NextVisit = FirstNonEmpty(parsed.NextVisit, fallback.NextVisit),
                ReferralCaseSummary = Clip(FirstNonEmpty(parsed.ReferralCaseSummary, fallback.ReferralCaseSummary), _settings.MaxNarrativeChars),
                ReferralReason = Clip(FirstNonEmpty(parsed.ReferralReason, fallback.ReferralReason), 80),
                Limitations = BuildLimitations(parsed.Limitations, missingSections, issues),
                GeneratedAtUtc = generatedAt
            };

            await SaveCacheAsync(
                patientId,
                generatedAt,
                dataFingerprint,
                result,
                cachedSummary,
                ct);

            return result;
        }

        private static async Task<T> SafeFetchAsync<T>(string source, Func<Task<T>> fetch, T fallback, ConcurrentBag<string> issues)
        {
            try
            {
                return await fetch();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                issues.Add($"{source} unavailable.");
                return fallback;
            }
        }

        private static int CalculateAge(DateOnly dob, DateTime asOfUtc)
        {
            var age = asOfUtc.Year - dob.Year;
            if (DateOnly.FromDateTime(asOfUtc) < dob.AddYears(age))
            {
                age--;
            }

            return Math.Max(age, 0);
        }

        private static List<string> BuildMissingSections(int allergyCount, int medicationCount, int labCount, int vitalsCount, int encounterCount, int appointmentCount)
        {
            var missing = new List<string>();
            if (allergyCount == 0) missing.Add("Allergy data not available.");
            if (medicationCount == 0) missing.Add("Medication data not available.");
            if (labCount == 0) missing.Add("Lab data not available.");
            if (vitalsCount == 0) missing.Add("Vitals data not available.");
            if (encounterCount == 0) missing.Add("Encounter history not available.");
            if (appointmentCount == 0) missing.Add("Appointment data not available.");
            return missing;
        }

        private PatientClinicalSummaryDto BuildFallback(
            PatientDto patient,
            DateTime generatedAt,
            IReadOnlyList<PatientReferral> referrals,
            IReadOnlyList<PatientEncounter> encounters,
            IReadOnlyList<PatientAppointmentDto> appointments,
            IReadOnlyList<string> missingSections,
            ConcurrentBag<string> issues)
        {
            var fullName = $"{patient.FirstName} {patient.LastName}".Trim();
            var activeConditions = referrals
                .SelectMany(x => SplitTokens(x.Diagnosis))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToList();

            var mostRecentConcern = referrals
                .OrderByDescending(x => x.DateUpdated)
                .Select(x => FirstNonEmpty(x.ReferralReason, x.Diagnosis, x.CaseSummary, x.CaseTitle))
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

            if (string.IsNullOrWhiteSpace(mostRecentConcern))
            {
                mostRecentConcern = encounters
                    .OrderByDescending(x => x.DateUpdated)
                    .Select(x => FirstNonEmpty(x.NoteTitle, x.NoteType, x.EncounterBody))
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            }

            mostRecentConcern = FirstNonEmpty(mostRecentConcern, "No acute concern identified from available records.");

            var nextVisit = BuildNextVisit(appointments, generatedAt);
            var narrative = $"{fullName} is a {CalculateAge(patient.DateOfBirth, generatedAt)}-year-old {FirstNonEmpty(patient.Gender, "patient").ToLowerInvariant()}. Most recent concern: {mostRecentConcern}. Next visit: {nextVisit}.";
            var referralReason = BuildReferralReason(mostRecentConcern);

            return new PatientClinicalSummaryDto
            {
                PatientId = patient.Id,
                Narrative = Clip(narrative, _settings.MaxNarrativeChars),
                ActiveConditions = activeConditions,
                MostRecentConcern = mostRecentConcern,
                CareGaps = missingSections.Take(6).ToList(),
                NextVisit = nextVisit,
                ReferralCaseSummary = Clip($"{fullName}: {mostRecentConcern} Next step: {nextVisit}.", _settings.MaxNarrativeChars),
                ReferralReason = referralReason,
                Limitations = BuildLimitations("Built from available structured chart fields.", missingSections, issues),
                GeneratedAtUtc = generatedAt
            };
        }

        private static string BuildNextVisit(IReadOnlyList<PatientAppointmentDto> appointments, DateTime nowUtc)
        {
            var upcoming = appointments.Where(x => x.AppointmentStartTime >= nowUtc).OrderBy(x => x.AppointmentStartTime).FirstOrDefault();
            if (upcoming == null) return "No upcoming appointment is currently scheduled.";
            return $"{upcoming.AppointmentStartTime:yyyy-MM-dd HH:mm} UTC ({FirstNonEmpty(upcoming.Reason, upcoming.AppointmentType)})";
        }

        private static IEnumerable<string> SplitTokens(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Enumerable.Empty<string>();
            }

            return value
                .Split([',', ';', '|', '/', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => x.Length is >= 3 and <= 80);
        }

        private static string BuildLimitations(string? modelLimitations, IEnumerable<string> missingSections, ConcurrentBag<string> issues)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(modelLimitations)) parts.Add(modelLimitations.Trim());
            parts.AddRange(missingSections);
            parts.AddRange(issues);
            return parts.Count == 0 ? "Summary generated from available chart data." : string.Join(" ", parts.Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static List<string> NormalizeList(IEnumerable<string>? list, IReadOnlyList<string> fallback)
        {
            var normalized = list?
                .Select(x => FirstNonEmpty(x))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToList()
                ?? new List<string>();

            return normalized.Count == 0 ? fallback.ToList() : normalized;
        }

        private static string FirstNonEmpty(params string?[] values)
            => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;

        private static string Clip(string? value, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var trimmed = value.Trim();
            return trimmed.Length <= maxChars ? trimmed : trimmed[..maxChars].TrimEnd() + "...";
        }

        private static ModelPayload? ParseModel(string raw)
        {
            var json = ExtractJson(raw);
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                return JsonSerializer.Deserialize<ModelPayload>(json, JsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string ExtractJson(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var text = raw.Trim();
            if (text.StartsWith("```", StringComparison.Ordinal))
            {
                var startLine = text.IndexOf('\n');
                var endFence = text.LastIndexOf("```", StringComparison.Ordinal);
                if (startLine >= 0 && endFence > startLine) text = text[(startLine + 1)..endFence].Trim();
            }

            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            return start >= 0 && end > start ? text.Substring(start, end - start + 1) : string.Empty;
        }

        private static string BuildPrompt()
        {
            return """
You are a clinician-focused chart summarizer.
Use only data present in the provided JSON context.
Do not invent clinical facts.
Return strict JSON only:
{
  "narrative": "string",
  "activeConditions": ["string"],
  "mostRecentConcern": "string",
  "careGaps": ["string"],
  "nextVisit": "string",
  "referralCaseSummary": "string",
  "referralReason": "string",
  "limitations": "string"
}
Quality:
- narrative should be 2-4 concise clinical sentences.
- referralCaseSummary should be suitable for referral workspace case summary.
- referralReason should be a concise 5-7 word referral reason.
- mention uncertainty in limitations when data is missing.
""";
        }

        private static string ComputeFingerprint(string value)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(bytes);
        }

        private static PatientClinicalSummaryDto ToDto(PatientClinicalSummaryCache cache)
        {
            return new PatientClinicalSummaryDto
            {
                PatientId = cache.PatientId == Guid.Empty ? cache.Id : cache.PatientId,
                Narrative = cache.Narrative ?? string.Empty,
                ActiveConditions = DeserializeList(cache.ActiveConditionsJson),
                MostRecentConcern = cache.MostRecentConcern ?? string.Empty,
                CareGaps = DeserializeList(cache.CareGapsJson),
                NextVisit = cache.NextVisit ?? string.Empty,
                ReferralCaseSummary = cache.ReferralCaseSummary ?? string.Empty,
                ReferralReason = cache.ReferralReason ?? string.Empty,
                Limitations = cache.Limitations ?? string.Empty,
                GeneratedAtUtc = cache.GeneratedAtUtc
            };
        }

        private async Task SaveCacheAsync(
            Guid patientId,
            DateTime generatedAtUtc,
            string fingerprint,
            PatientClinicalSummaryDto summary,
            PatientClinicalSummaryCache? existingCache,
            CancellationToken ct)
        {
            var cache = existingCache ?? new PatientClinicalSummaryCache
            {
                Id = patientId,
                TenantId = _userContext.TenantId,
                PatientId = patientId,
                PartitionKey = EntityKeyPolicy.TenantPatientPartition(_userContext.TenantId, patientId),
                RowKey = EntityKeyPolicy.Row(patientId)
            };

            cache.Id = patientId;
            cache.TenantId = _userContext.TenantId;
            cache.PatientId = patientId;
            cache.PartitionKey = string.IsNullOrWhiteSpace(cache.PartitionKey)
                ? EntityKeyPolicy.TenantPatientPartition(_userContext.TenantId, patientId)
                : cache.PartitionKey;
            cache.RowKey = string.IsNullOrWhiteSpace(cache.RowKey)
                ? EntityKeyPolicy.Row(patientId)
                : cache.RowKey;

            cache.DataFingerprint = fingerprint;
            cache.GeneratedAtUtc = generatedAtUtc;
            cache.Narrative = summary.Narrative ?? string.Empty;
            cache.ActiveConditionsJson = SerializeList(summary.ActiveConditions);
            cache.MostRecentConcern = summary.MostRecentConcern ?? string.Empty;
            cache.CareGapsJson = SerializeList(summary.CareGaps);
            cache.NextVisit = summary.NextVisit ?? string.Empty;
            cache.ReferralCaseSummary = summary.ReferralCaseSummary ?? string.Empty;
            cache.ReferralReason = summary.ReferralReason ?? string.Empty;
            cache.Limitations = summary.Limitations ?? string.Empty;
            cache.IsDeleted = false;

            await _patientClinicalSummaryCacheRepository.SaveAsync(cache, ct);
        }

        private static string SerializeList(IEnumerable<string>? values)
            => JsonSerializer.Serialize(
                values?
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Select(v => v.Trim())
                    .ToList()
                ?? new List<string>(),
                JsonOptions);

        private static List<string> DeserializeList(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new List<string>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<string>>(raw, JsonOptions) ?? new List<string>();
            }
            catch (JsonException)
            {
                return new List<string>();
            }
        }

        private sealed class ModelPayload
        {
            public string Narrative { get; set; } = string.Empty;
            public List<string> ActiveConditions { get; set; } = new();
            public string MostRecentConcern { get; set; } = string.Empty;
            public List<string> CareGaps { get; set; } = new();
            public string NextVisit { get; set; } = string.Empty;
            public string ReferralCaseSummary { get; set; } = string.Empty;
            public string ReferralReason { get; set; } = string.Empty;
            public string Limitations { get; set; } = string.Empty;
        }

        private static string BuildReferralReason(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var words = value
                .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(7);

            return string.Join(" ", words);
        }
    }
}
