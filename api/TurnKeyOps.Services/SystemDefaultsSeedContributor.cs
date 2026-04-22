using System.Text.Json;
using MedInsights.Lib;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;

namespace MedInsights.Services
{
    public sealed class SystemDefaultsSeedContributor : IStartupSeedContributor
    {
        private static readonly Guid OfficeVisitNoteTypeId = Guid.Parse("4B4B98CA-209C-4FB3-8F46-95A0355D87A2");
        private static readonly Guid FollowUpNoteTypeId = Guid.Parse("4D82742D-4731-46D6-B0DE-F0E8E5885F55");
        private static readonly Guid TelehealthNoteTypeId = Guid.Parse("4AF53BD4-C163-49E2-975F-E4A15A291FC6");
        private static readonly Guid AnnualWellnessNoteTypeId = Guid.Parse("758A3A97-1E58-46A8-B4D5-D207C157B0FA");
        private static readonly Guid ConsultationNoteTypeId = Guid.Parse("C354B4D4-C679-4C97-97F7-F795BF4D155D");

        private static readonly NoteTypeSeed[] SystemSeeds =
        [
            new(OfficeVisitNoteTypeId, "Office Visit", "OFFICE_VISIT", "Standard in-office evaluation and management note.", true, 10),
            new(FollowUpNoteTypeId, "Follow-Up", "FOLLOW_UP", "Follow-up note focused on interval change, response to treatment, and next steps.", false, 20),
            new(TelehealthNoteTypeId, "Telehealth", "TELEHEALTH", "Virtual or remote patient evaluation note.", false, 30),
            new(AnnualWellnessNoteTypeId, "Annual Wellness", "ANNUAL_WELLNESS", "Preventive annual wellness visit note.", false, 40),
            new(ConsultationNoteTypeId, "Consultation", "CONSULTATION", "Consult note for initial specialist or requested evaluation.", false, 50)
        ];

        private static readonly NoteTypeProfileSeed[] SystemProfileSeeds =
        [
            new(
                OfficeVisitNoteTypeId,
                "Generate a concise, clinically relevant office visit note. Emphasize the assessment and plan, include pertinent positives and negatives, avoid unnecessary repetition, and include patient education and follow-up instructions when appropriate.",
                SerializeSections(
                [
                    new("chief_complaint", "Chief Complaint", 1, true, true, "State the patient's primary reason for the visit in a brief, clinically clear way."),
                    new("hpi", "HPI", 2, true, true, "Summarize symptom onset, duration, severity, context, modifying factors, and associated symptoms relevant to the encounter."),
                    new("exam", "Exam", 3, true, true, "Document pertinent physical exam findings and omit unnecessary normal detail unless clinically helpful."),
                    new("assessment", "Assessment", 4, true, true, "Summarize the clinical impression and medical decision making clearly."),
                    new("plan", "Plan", 5, true, true, "Include treatment decisions, medications, testing, referrals, return precautions, and follow-up.")
                ]),
                false,
                false),

            new(
                FollowUpNoteTypeId,
                "Generate a focused follow-up note. Emphasize interval change since the last visit, treatment response, current status, and the updated plan. Keep the note concise and action-oriented.",
                SerializeSections(
                [
                    new("interval_history", "Interval History", 1, true, true, "Describe changes since the last visit, including symptom progression, treatment response, adherence, and any new concerns."),
                    new("exam", "Exam", 2, false, true, "Include updated pertinent exam findings when relevant to the follow-up problem."),
                    new("assessment", "Assessment", 3, true, true, "Summarize current status, progress, and ongoing clinical impression."),
                    new("plan", "Plan", 4, true, true, "Include medication changes, monitoring, testing, referrals, and follow-up timing.")
                ]),
                false,
                false),

            new(
                TelehealthNoteTypeId,
                "Generate a telehealth note that is concise, clinically clear, and appropriate for a virtual encounter. Include telehealth-specific limitations, emphasize assessment and plan, and document any escalation or in-person follow-up needs.",
                SerializeSections(
                [
                    new("chief_complaint", "Chief Complaint", 1, true, true, "State the primary reason for the telehealth encounter."),
                    new("hpi", "HPI", 2, true, true, "Summarize the patient's symptoms and relevant context as reported during the virtual visit."),
                    new("limited_exam", "Limited Exam", 3, true, true, "Document observational findings available virtually and clearly note limitations of remote examination."),
                    new("assessment", "Assessment", 4, true, true, "Summarize clinical impression and decision making based on the information available in the telehealth setting."),
                    new("plan", "Plan", 5, true, true, "Include treatment plan, escalation guidance, return precautions, and follow-up instructions.")
                ]),
                true,
                false),

            new(
                AnnualWellnessNoteTypeId,
                "Generate a preventive care-focused annual wellness note. Emphasize screening status, risk factor review, counseling, health maintenance, and a clear preventive plan.",
                SerializeSections(
                [
                    new("preventive_review", "Preventive Review", 1, true, true, "Summarize preventive care topics and health maintenance items addressed during the visit."),
                    new("screenings", "Screenings", 2, true, true, "Document completed, due, recommended, or declined screenings and preventive services."),
                    new("risk_assessment", "Risk Assessment", 3, true, true, "Highlight relevant lifestyle, family history, chronic disease risk, safety concerns, and prevention opportunities."),
                    new("assessment", "Assessment", 4, true, true, "Summarize overall wellness status and key preventive concerns."),
                    new("plan", "Plan", 5, true, true, "Include counseling, screening recommendations, immunizations, preventive interventions, and follow-up.")
                ]),
                false,
                true),

            new(
                ConsultationNoteTypeId,
                "Generate a specialist-style consultation note that clearly states the reason for consult, synthesizes the relevant history and findings, presents the impression up front, and ends with clear recommendations for the requesting clinician and patient.",
                SerializeSections(
                [
                    new("assessment", "Assessment / Impression", 1, true, true, "Lead with the consultant's synthesis, impression, and key clinical reasoning."),
                    new("recommendations", "Recommendations", 2, true, true, "Provide clear, actionable recommendations, next steps, workup, treatment considerations, and follow-up guidance."),
                    new("reason_for_consult", "Reason for Consult", 3, true, true, "State the consultation request, referral question, or reason for specialist evaluation."),
                    new("history", "Relevant History", 4, true, true, "Summarize the most relevant medical history, symptoms, prior workup, and context related to the consultation question."),
                    new("findings", "Findings", 5, true, true, "Document pertinent exam findings, diagnostics, imaging, labs, or other clinical data relevant to the consultation.")
                ]),
                false,
                false)
        ];

        private readonly INoteTypeRepository _noteTypeRepository;
        private readonly INoteTypeProfileRepository _noteTypeProfileRepository;

        public SystemDefaultsSeedContributor(
            INoteTypeRepository noteTypeRepository,
            INoteTypeProfileRepository noteTypeProfileRepository)
        {
            _noteTypeRepository = noteTypeRepository;
            _noteTypeProfileRepository = noteTypeProfileRepository;
        }

        public async Task SeedAsync(CancellationToken ct = default)
        {
            foreach (var seed in SystemSeeds)
            {
                var noteType = await EnsureSystemNoteTypeAsync(seed, ct);
                var profileSeed = SystemProfileSeeds.Single(x => x.NoteTypeId == noteType.Id);
                await EnsureSystemProfileAsync(noteType, profileSeed, ct);
            }
        }

        private async Task<NoteType> EnsureSystemNoteTypeAsync(NoteTypeSeed seed, CancellationToken ct)
        {
            var existing = await _noteTypeRepository.GetSystemDefinitionAsync(seed.Id, ct);
            if (existing is null)
            {
                var created = new NoteType
                {
                    Id = seed.Id,
                    PartitionKey = NoteTypeRepository.SystemPartitionKey,
                    RowKey = EntityKeyPolicy.Row(seed.Id),
                    RecordType = NoteTypeRepository.DefinitionRecordType,
                    Name = seed.Name,
                    Code = seed.Code,
                    NormalizedCode = seed.Code,
                    Description = seed.Description,
                    IsSystem = true,
                    IsEnabled = true,
                    IsDefault = seed.IsDefault,
                    SortOrder = seed.SortOrder,
                    CreatedBy = "system",
                    DateCreated = DateTime.UtcNow,
                    UpdatedBy = "system",
                    DateUpdated = DateTime.UtcNow
                };

                await _noteTypeRepository.SaveAsync(created, ct);
                return created;
            }

            var shouldUpdate =
                existing.Name != seed.Name ||
                existing.Code != seed.Code ||
                existing.NormalizedCode != seed.Code ||
                existing.Description != seed.Description ||
                existing.SortOrder != seed.SortOrder ||
                existing.IsDefault != seed.IsDefault ||
                !existing.IsEnabled;

            if (!shouldUpdate)
                return existing;

            existing.Name = seed.Name;
            existing.Code = seed.Code;
            existing.NormalizedCode = seed.Code;
            existing.Description = seed.Description;
            existing.IsEnabled = true;
            existing.IsDefault = seed.IsDefault;
            existing.SortOrder = seed.SortOrder;
            existing.UpdatedBy = "system";
            existing.DateUpdated = DateTime.UtcNow;

            await _noteTypeRepository.SaveAsync(existing, ct);
            return existing;
        }

        private async Task EnsureSystemProfileAsync(NoteType noteType, NoteTypeProfileSeed seed, CancellationToken ct)
        {
            var existing = await _noteTypeProfileRepository.GetSystemProfileByNoteTypeIdAsync(noteType.Id, ct);
            if (existing is null)
            {
                await _noteTypeProfileRepository.SaveAsync(new NoteTypeProfile
                {
                    Id = noteType.Id,
                    PartitionKey = NoteTypeProfileRepository.SystemPartitionKey,
                    RowKey = EntityKeyPolicy.Row(noteType.Id),
                    TenantId = null,
                    NoteTypeId = noteType.Id,
                    RecordType = NoteTypeProfileRepository.ProfileRecordType,
                    PromptInstructions = seed.PromptInstructions,
                    SectionSchemaJson = seed.SectionSchemaJson,
                    RequireTelehealthAttestation = seed.RequireTelehealthAttestation,
                    RequirePreventiveReview = seed.RequirePreventiveReview,
                    IsSystem = true,
                    IsDeleted = false,
                    CreatedBy = "system",
                    DateCreated = DateTime.UtcNow,
                    UpdatedBy = "system",
                    DateUpdated = DateTime.UtcNow
                }, ct);
                return;
            }

            var shouldUpdate =
                existing.RecordType != NoteTypeProfileRepository.ProfileRecordType ||
                existing.PromptInstructions != seed.PromptInstructions ||
                existing.SectionSchemaJson != seed.SectionSchemaJson ||
                existing.RequireTelehealthAttestation != seed.RequireTelehealthAttestation ||
                existing.RequirePreventiveReview != seed.RequirePreventiveReview ||
                !existing.IsSystem;

            if (!shouldUpdate)
                return;

            existing.RecordType = NoteTypeProfileRepository.ProfileRecordType;
            existing.PromptInstructions = seed.PromptInstructions;
            existing.SectionSchemaJson = seed.SectionSchemaJson;
            existing.RequireTelehealthAttestation = seed.RequireTelehealthAttestation;
            existing.RequirePreventiveReview = seed.RequirePreventiveReview;
            existing.IsSystem = true;
            existing.UpdatedBy = "system";
            existing.DateUpdated = DateTime.UtcNow;

            await _noteTypeProfileRepository.SaveAsync(existing, ct);
        }

        private static string SerializeSections(IEnumerable<SectionSeed> sections, bool alwaysCreateReferral = false)
        {
            var materialized = sections.ToList();

            if (!materialized.Any(section => string.Equals(section.OutputTarget, "billing_recommendations", StringComparison.OrdinalIgnoreCase)))
            {
                materialized.Add(new SectionSeed(
                    "billing_recommendations",
                    "Billing Recommendations",
                    900,
                    true,
                    true,
                    "Recommend billing codes only when supported by transcript evidence. If insufficient data exists, explicitly state what is missing.",
                    "billing_recommendations"));
            }

            if (!materialized.Any(section => string.Equals(section.OutputTarget, "external_communication", StringComparison.OrdinalIgnoreCase)))
            {
                materialized.Add(new SectionSeed(
                    "external_communication",
                    "External Communication",
                    1000,
                    false,
                    false,
                    "Create patient-friendly communication with summary, options discussed, and next steps when this section is enabled.",
                    "external_communication",
                    "email"));
            }

            return JsonSerializer.Serialize(new
            {
                sections = materialized.Select(section => new
                {
                    key = section.Key,
                    label = section.Label,
                    sortOrder = section.SortOrder,
                    isRequired = section.IsRequired,
                    isEnabled = section.IsEnabled,
                    promptInstructions = section.PromptInstructions,
                    outputTarget = section.OutputTarget,
                    communicationMode = section.CommunicationMode
                }),
                tags = alwaysCreateReferral
                    ? new[] { "AlwaysCreateReferral" }
                    : Array.Empty<string>()
            });
        }

        private sealed record NoteTypeSeed(Guid Id, string Name, string Code, string Description, bool IsDefault, int SortOrder);

        private sealed record NoteTypeProfileSeed(
            Guid NoteTypeId,
            string PromptInstructions,
            string SectionSchemaJson,
            bool RequireTelehealthAttestation,
            bool RequirePreventiveReview);

        private sealed record SectionSeed(
            string Key,
            string Label,
            int SortOrder,
            bool IsRequired,
            bool IsEnabled,
            string PromptInstructions,
            string OutputTarget = "clinical_note",
            string? CommunicationMode = null);
    }
}
