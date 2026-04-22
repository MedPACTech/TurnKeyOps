using OpenAIChatMessage = OpenAI.Chat.ChatMessage;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Models;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.BackgroundServices.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;
using MedInsights.AzureServices.Interfaces;

namespace MedInsights.Services
{
    public class PatientEncounterService : IPatientEncounterService
    {
        private readonly IPatientEncounterRepository _encounterRepository;
        private readonly IUserContext _userContext;
        private readonly IAzureBlobStorageService _azureBlobStorageService;
        private readonly IEncounterTranscriptionQueue _queue;
        private readonly IAIService<OpenAIChatMessage> _ai;
        private readonly IPatientService _patientService;
        private readonly INoteTypePromptBuilderService _noteTypePromptBuilderService;


        public PatientEncounterService(
            IPatientEncounterRepository patientEncounterRepository,
            IUserContext userContext,
            IAzureBlobStorageService azureBlobStorageService,
            IEncounterTranscriptionQueue transcriptionQueue,
            IAIService<OpenAIChatMessage> ai,
            IPatientService patientService,
            INoteTypePromptBuilderService noteTypePromptBuilderService
            )
        {
            _encounterRepository = patientEncounterRepository;
            _userContext = userContext;
            _queue = transcriptionQueue;
            _azureBlobStorageService = azureBlobStorageService;
            _ai = ai;
            _patientService = patientService;
            _noteTypePromptBuilderService = noteTypePromptBuilderService;
        }

        //TODO: Another spot check here but add this to a base service later?
        private string PartitionKeyForCurrent()
        {
            return EntityKeyPolicy.TenantUserPartition(_userContext.TenantId, _userContext.UserId);
        }

        private string PartitionKeyForPatient(Guid patientId)
        {
            return EntityKeyPolicy.TenantPatientPartition(_userContext.TenantId, patientId);
        }

        //TODO: Another base service later option?
        private string RowKeyForCurrent(Guid id)
        {
            return EntityKeyPolicy.Row(id);
        }

        public async Task<PatientEncounterDto> AddEncounterAsync(Stream audioStream, Guid? patientId, CancellationToken ct)
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            var partitionKey = patientId.HasValue
                ? PartitionKeyForPatient(patientId.Value)
                : PartitionKeyForCurrent();
            var encounterGuid = Guid.NewGuid();
            var encounterId = RowKeyForCurrent(encounterGuid);
            var fileName = $"{encounterId}.webm";

            // Save audio blob
            using var ms = new MemoryStream();
            await audioStream.CopyToAsync(ms, ct);
            var audioBytes = ms.ToArray();

            await _azureBlobStorageService.Save("encounters", fileName, audioBytes);

            // Create table entity
            var encounter = new PatientEncounter
            {
                Id = encounterGuid,
                PartitionKey = partitionKey,
                RowKey = encounterId,
                DateCreated = DateTime.UtcNow,
                DateUpdated = DateTime.UtcNow,
                PatientId = patientId.HasValue ? EntityKeyPolicy.Row(patientId.Value) : null,
                ProviderId = _userContext.UserId,
                NoteType = "AudioCapture",
                NoteTitle = "Audio Capture",
                Data = $"{{\"audioFileUrl\":\"{fileName}\"}}",
                Status = "Pending",
                EncounterBody = string.Empty,
                IsDeleted = false
            };

            encounter = await _encounterRepository.SaveAsync(encounter, ct);

            // Queue transcription by reference
            await _queue.QueueJobAsync(new EncounterTranscriptionJob(partitionKey, encounter.RowKey));

            return PatientEncounterMapper.ToDto(encounter);
        }

        public async Task<PatientEncounterDto> AddEncounterFromNarrativeAsync(PatientEncounterNarrativeCreateRequestDto dto, CancellationToken ct)
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var narrative = (dto.NarrativeText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(narrative))
                throw new InvalidOperationException("Narrative text is required.");

            var partitionKey = PartitionKeyForPatient(dto.PatientId);
            var encounterId = Guid.NewGuid();
            var encounterRowKey = RowKeyForCurrent(encounterId);

            var encounter = new PatientEncounter
            {
                Id = encounterId,
                PartitionKey = partitionKey,
                RowKey = encounterRowKey,
                DateCreated = DateTime.UtcNow,
                DateUpdated = DateTime.UtcNow,
                PatientId = EntityKeyPolicy.Row(dto.PatientId),
                ProviderId = _userContext.UserId,
                NoteType = string.IsNullOrWhiteSpace(dto.Template) ? "Narrative" : dto.Template,
                NoteTitle = "Narrative Encounter",
                Data = "{\"source\":\"narrative\"}",
                EncounterBody = narrative,
                Status = "Ready",
                IsDeleted = false
            };

            encounter = await _encounterRepository.SaveAsync(encounter, ct);

            return PatientEncounterMapper.ToDto(encounter);
        }

        public async Task<PatientEncounterNoteResponseDto> GenerateNoteAsync(PatientEncounterNoteRequestDto dto, CancellationToken ct = default)
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            var rowKey = RowKeyForCurrent(dto.EncounterId);
            var partitionKey = PartitionKeyForPatient(dto.PatientId);
            var encounter = await _encounterRepository.GetAsync(partitionKey, rowKey);
            if (encounter == null)
                encounter = await _encounterRepository.GetByRowKeyAsync(rowKey, ct);
            if (encounter == null)
                throw new KeyNotFoundException("Encounter not found.");

            var transcript = !string.IsNullOrWhiteSpace(encounter.EncounterBody)
                ? encounter.EncounterBody
                : dto.Transcript;

            if (string.IsNullOrWhiteSpace(transcript))
                throw new InvalidOperationException("Transcript is not available yet.");

            var promptProfile = await _noteTypePromptBuilderService.ResolveAsync(dto.Template, ct);
            var systemPrompt = _noteTypePromptBuilderService.BuildSystemPrompt(promptProfile);

            var userMessages = new[]
            {
                $"Template: {promptProfile.DisplayName}",
                $"PatientId: {dto.PatientId}",
                "Transcript:\n" + transcript
            };

            const int maxTokens = 1400;
            const double temperature = 0.1;

            var noteText = await _ai.GetChatCompletionAsync(
                systemPrompt: systemPrompt,
                userMessages: userMessages,
                maxOutputTokens: maxTokens,
                temperature: temperature,
                ct: ct
            );

            var cleaned = (noteText ?? string.Empty).Trim();

            return new PatientEncounterNoteResponseDto
            {
                EncounterId = dto.EncounterId,
                PatientId = dto.PatientId,
                Template = dto.Template,
                NoteText = cleaned,
                GeneratedAt = DateTimeOffset.UtcNow
            };
        }


        public async Task<PatientEncounterDto?> GetAsync(Guid id)
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            var rowKey = RowKeyForCurrent(id);
            var encounter = await _encounterRepository.GetByRowKeyAsync(rowKey);
            return encounter != null ? PatientEncounterMapper.ToDto(encounter) : null;
        }

        //TODO: Differentiate between services that are user scoped vs tenant scoped
        public async Task<List<PatientEncounterDto>> GetMyEncountersAsync()
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            var partitionKey = PartitionKeyForCurrent();
            var encounters = await _encounterRepository.GetByPartitionAsync(partitionKey);
            return encounters.Select(PatientEncounterMapper.ToDto).ToList();
        }

        public async Task<PatientEncounterDto> UpdateAsync(PatientEncounterDto dto)
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            var partitionKey = PartitionKeyForCurrent();
            if (!string.IsNullOrWhiteSpace(dto.PatientId) && Guid.TryParse(dto.PatientId, out var patientId))
            {
                partitionKey = PartitionKeyForPatient(patientId);
            }
            else
            {
                var existing = await _encounterRepository.GetByRowKeyAsync(RowKeyForCurrent(dto.Id));
                if (existing != null && !string.IsNullOrWhiteSpace(existing.PartitionKey))
                    partitionKey = existing.PartitionKey;
            }
            var entity = PatientEncounterMapper.ToEntity(dto, partitionKey);

            var saved = await _encounterRepository.SaveAsync(entity);

            return PatientEncounterMapper.ToDto(saved);
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            // if (!_userContext.IsAuthenticated)
            //     throw new UnauthorizedAccessException();

            // var partitionKey = PartitionKeyForCurrent();
            // var rowKey = RowKeyForCurrent(id);
            // var encounter = await _encounterRepository.GetAsync(partitionKey, rowKey)
            //               ?? throw new KeyNotFoundException("Encounter not found.");

            // encounter.IsDeleted = true;
            // encounter.DateUpdated = DateTime.UtcNow;

            // await _encounterRepository.DeleteSoftAsync(encounter, ct: CancellationToken.None);

            var encounter = await _encounterRepository.GetByRowKeyAsync(RowKeyForCurrent(id), ct)
                          ?? throw new KeyNotFoundException("Encounter not found.");

            encounter.IsDeleted = true;
            encounter.DateUpdated = DateTime.UtcNow;
            await _encounterRepository.SaveAsync(encounter, ct);

            return true;
        }

       public async Task<List<PatientEncounterListItemDto>> GetMyEncounterListAsync()
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            var partitionKey = PartitionKeyForCurrent();

            // 1) Load encounters
            var encounters = (await _encounterRepository.GetByPartitionAsync(partitionKey)).ToList();

            // 2) Collect distinct patient ids
            var patientIds = encounters
                .Select(e => e.PatientId)
                .Where(pid => !string.IsNullOrWhiteSpace(pid))
                .Select(pid => Guid.TryParse(pid, out var g) ? (Guid?)g : null)
                .Where(g => g.HasValue)
                .Select(g => g!.Value)
                .Distinct()
                .ToList();


            // 3) Fetch patients in batch
            var patientsById = patientIds.Count > 0
                ? await _patientService.GetByIdsAsync(patientIds)
                : new Dictionary<Guid, PatientDto>();

            // 4) Project into list DTO
            return encounters.Select(e =>
            {
                PatientDto? patient = null;

                Guid? pid = null;
                if (!string.IsNullOrWhiteSpace(e.PatientId) && Guid.TryParse(e.PatientId, out var g))
                {
                    pid = g;
                    patientsById.TryGetValue(g, out patient);
                }

                return new PatientEncounterListItemDto
                {
                    Id = e.Id == Guid.Empty ? Guid.Parse(e.RowKey) : e.Id,
                    PatientId = pid?.ToString("D"),        // or pid if DTO uses Guid?
                    PatientFirstName = patient?.FirstName,
                    PatientLastName = patient?.LastName,
                    CreatedAt = e.DateCreated,
                    UpdatedAt = e.DateUpdated,
                    Status = e.Status
                };
            }).ToList();

        }


    }
    
    
}




