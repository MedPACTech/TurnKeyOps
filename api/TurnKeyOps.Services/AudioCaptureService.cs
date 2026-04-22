using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Models;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.BackgroundServices.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;
using MedInsights.AzureServices.Interfaces;
using Microsoft.Extensions.Options;

namespace MedInsights.Services
{
    public class AudioCaptureService : IAudioCaptureService
    {
        private readonly IAudioCaptureRepository _audioCaptureRepository;
        private readonly IUserContext _userContext;
        private readonly IAzureBlobStorageService _azureBlobStorageService;
        private readonly IAudioCaptureTranscriptionQueue _queue;

        public AudioCaptureService(
            IAudioCaptureRepository audioCaptureRepository,
            IUserContext userContext,
            IAzureBlobStorageService azureBlobStorageService,
            IAudioCaptureTranscriptionQueue transcriptionQueue
           )
        {
            _audioCaptureRepository = audioCaptureRepository;
            _userContext = userContext;
            _queue = transcriptionQueue;
            _azureBlobStorageService = azureBlobStorageService;
        }

        //TODO: Another spot check here but add this to a base service later?
        private string PartitionKeyForCurrent()
        {
            return EntityKeyPolicy.TenantUserPartition(_userContext.TenantId, _userContext.UserId);
        }

        //TODO: Another base service later option?
        private string RowKeyForCurrent(Guid id)
        {
            return EntityKeyPolicy.Row(id);
        }

        public async Task<AudioCaptureDto> StartCaptureAsync(string? patientId, CancellationToken ct)
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            var partitionKey = PartitionKeyForCurrent();

            // Generate Id now so UI can record against it
            var id = Guid.NewGuid();
            var rowKey = RowKeyForCurrent(id);

            var audioCapture = new AudioCapture
            {
                Id               = id,
                PartitionKey     = partitionKey,
                RowKey           = rowKey,

                PatientId        = patientId ?? string.Empty,

                // No audio yet (recording local)
                AudioFileUrl     = string.Empty,

                // Initial lifecycle state
                Status           = "Recording",
                ProcessingStage  = "Recording",

                RetryCount       = 0,
                TranscribedText  = string.Empty,
                SpeechTokenCount = null,
                EstimatedCostUsd = null,

                DateCreated      = DateTimeOffset.UtcNow,
                DateUpdated      = DateTimeOffset.UtcNow,

                IsDeleted        = false
            };

            audioCapture = await _audioCaptureRepository.SaveAsync(audioCapture, ct);

            return AudioCaptureMapper.ToDto(audioCapture);
        }


            public async Task<AudioCaptureDto> AddOrAttachCaptureAsync(
            Stream audioStream,
            Guid? captureId,
            CancellationToken ct)
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            var partitionKey = PartitionKeyForCurrent();

            // Read audio bytes once
            using var ms = new MemoryStream();
            await audioStream.CopyToAsync(ms, ct);
            var audioBytes = ms.ToArray();

            AudioCapture audioCapture;
            string rowKey;
            string fileName;

            if (!captureId.HasValue || captureId.Value == Guid.Empty)
            {
                // ---------------- CREATE NEW ----------------
                var newId = Guid.NewGuid();
                rowKey   = RowKeyForCurrent(newId);
                fileName = $"{rowKey}.webm";

                await _azureBlobStorageService.Save("audio-captures", fileName, audioBytes);

                audioCapture = new AudioCapture
                {
                    Id               = newId,
                    PartitionKey     = partitionKey,
                    RowKey           = rowKey,

                    PatientId        = string.Empty,
                    AudioFileUrl     = fileName,

                    Status           = "Pending",
                    ProcessingStage  = "Uploaded",
                    RetryCount       = 0,
                    TranscribedText  = string.Empty,
                    SpeechTokenCount = null,
                    EstimatedCostUsd = null,

                    DateCreated      = DateTimeOffset.UtcNow,
                    DateUpdated      = DateTimeOffset.UtcNow,
                    IsDeleted        = false
                };

            }
            else
            {
                // ---------------- ATTACH TO EXISTING ----------------
                rowKey = RowKeyForCurrent(captureId.Value);

                audioCapture = await _audioCaptureRepository.GetAsync(partitionKey, rowKey);
                if (audioCapture == null)
                    throw new KeyNotFoundException($"Capture {captureId} not found.");

                // Overwrite existing blob name if already set, else use rowKey-based filename
                fileName = string.IsNullOrWhiteSpace(audioCapture.AudioFileUrl)
                    ? $"{rowKey}.webm"
                    : audioCapture.AudioFileUrl;

                await _azureBlobStorageService.Save("audio-captures", fileName, audioBytes);

                // Reset pipeline fields for a fresh transcription
                audioCapture.AudioFileUrl     = fileName;
                audioCapture.Status           = "Pending";
                audioCapture.ProcessingStage  = "Uploaded";
                audioCapture.RetryCount       = 0;
                audioCapture.SpeechTokenCount = null;
                audioCapture.EstimatedCostUsd = null;
                audioCapture.TranscribedText  = string.Empty;
                audioCapture.DateUpdated      = DateTimeOffset.UtcNow;
            }

            // Generate a JobToken for this transcription run
            var jobToken = Guid.NewGuid().ToString("N");

            // Move to "Transcribing" state before saving
            audioCapture.Status          = "Transcribing";
            audioCapture.ProcessingStage = "Transcribing";
            audioCapture.JobToken        = jobToken;
            audioCapture.DateUpdated     = DateTimeOffset.UtcNow;

            // Persist once (no Add+Update ping-pong, so no empty ETag on Update)
            audioCapture = await _audioCaptureRepository.SaveAsync(audioCapture, ct);

            // Build the queue job for the worker
            var job = new AudioCaptureTranscriptionJob
            {
                PartitionKey       = partitionKey,
                RowKey             = audioCapture.RowKey,

                AudioBlobContainer = "audio-captures",
                AudioBlobName      = fileName,

                // TODO: this is an absolute URL right now – fine for local testing,
                // but later consider making this a relative path and using HttpClient.BaseAddress.
                CallbackPath       = $"http://localhost:5178/api/audiocaptures/transcription/{Uri.EscapeDataString(audioCapture.RowKey)}",

                JobToken           = jobToken,
                Scenario           = "audio-capture"
            };

            await _queue.QueueJobAsync(job);

            return AudioCaptureMapper.ToDto(audioCapture);
        }


        public async Task<AudioCaptureDto?> GetAsync(Guid id, CancellationToken ct)
        {
            //TODO: spot check here but add this to a base service later?
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            var partitionKey = PartitionKeyForCurrent();
            var rowKey = RowKeyForCurrent(id);
            var audioCapture = await _audioCaptureRepository.GetAsync(partitionKey, rowKey);
            return audioCapture != null ? AudioCaptureMapper.ToDto(audioCapture) : null;
        }

        //TODO: Differentiate between services that are user scoped vs tenant scoped
        public async Task<List<AudioCaptureDto>> GetMyCapturesAsync(CancellationToken ct)
        {
            //TODO: spot check here but add this to a base service later?
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            var partitionKey = PartitionKeyForCurrent();
            var entities = await _audioCaptureRepository.GetByPartitionAsync(partitionKey, ct);
            return entities.Select(AudioCaptureMapper.ToDto).ToList();
        }

        public async Task<AudioCaptureDto> UpdateAsync(AudioCaptureDto dto, CancellationToken ct)
        {

            //TODO: Function App Calls This Without Auth, So Only Enforce If No JobToken
            //if (!_userContext.IsAuthenticated && !string.IsNullOrWhiteSpace(dto.JobToken))
            //    throw new UnauthorizedAccessException();

            string? partitionKey;
            string? rowKey;

            if (!string.IsNullOrWhiteSpace(dto.JobToken))
            {
                partitionKey = dto.JobKey;
                rowKey = EntityKeyPolicy.Row(dto.Id);
            }
            else
            {
                if (!_userContext.IsAuthenticated)
                    throw new UnauthorizedAccessException();

                partitionKey = PartitionKeyForCurrent();
                rowKey = RowKeyForCurrent(dto.Id);
            }

            //get the existing entity to preserve created date
            var capture = await _audioCaptureRepository.GetAsync(partitionKey, rowKey, ct)
                ?? throw new KeyNotFoundException($"AudioCapture {dto.Id} not found.");    

            //Validate JobToken if provided
            if (!string.IsNullOrWhiteSpace(dto.JobToken) && dto.JobToken != capture.JobToken)
                throw new UnauthorizedAccessException("Invalid JobToken.");

            //clear JobToken if updating from worker
            if (!string.IsNullOrWhiteSpace(dto.JobToken))
            {
                dto.JobToken = string.Empty;
                dto.JobKey = string.Empty;
            }

            var entity = AudioCaptureMapper.ToEntity(dto, partitionKey);
            entity.DateCreated = capture.DateCreated; //preserve original created date
            entity.DateUpdated = DateTimeOffset.UtcNow;

            var saved = await _audioCaptureRepository.SaveAsync(entity, ct);

            return AudioCaptureMapper.ToDto(saved);
        }

        public async Task<bool> UpdateTranscriptionAsync(AudioCaptureTranscriptDto dto, CancellationToken ct)
        {
            
            if (string.IsNullOrWhiteSpace(dto.JobToken) || string.IsNullOrWhiteSpace(dto.JobKey))
            {
               throw new UnauthorizedAccessException();
            }

            var partitionKey = dto.JobKey;
            var rowKey = EntityKeyPolicy.Row(dto.Id);
  
            //get the existing entity to preserve created date
            var capture = await _audioCaptureRepository.GetAsync(partitionKey, rowKey, ct)
                ?? throw new KeyNotFoundException($"AudioCapture {dto.Id} not found.");    

            //Validate JobToken if provided
            if (dto.JobToken != capture.JobToken)
                throw new UnauthorizedAccessException("Invalid JobToken.");

            //clear JobToken if updating from worker
            capture.JobToken = string.Empty;
            capture.DateUpdated = DateTimeOffset.UtcNow;
            capture.Status = dto.Status;
            capture.TranscribedText = dto.TranscribedText;
            capture.ProcessingStage = "TranscriptionCompleted";

            try
            {
                // Wrap in try-catch to log or handle specific errors if needed
                 await _audioCaptureRepository.SaveAsync(capture, ct);
            }
            catch (Exception ex)
            {
                // Log the exception or handle it as needed
                throw; // Re-throwing for now
            }

            return true;
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            //     //TODO: spot check here but add this to a base service later?
            //     if (!_userContext.IsAuthenticated)
            //         throw new UnauthorizedAccessException();

            //     var partitionKey = PartitionKeyForCurrent();
            //     var rowKey = EntityKeyPolicy.Row(id);
            //     var dictation = await _dictationRepository.GetAsync(partitionKey, rowKey, ct)
            //                 ?? throw new KeyNotFoundException("Dictation not found.");

            //     dictation.IsDeleted = true;
            //     dictation.DateUpdated = DateTime.UtcNow;

            //     await _dictationRepository.DeleteSoftAsync(dictation, ct);

            var capture = await _audioCaptureRepository.GetByRowKeyAsync(EntityKeyPolicy.Row(id), ct)
                        ?? throw new KeyNotFoundException("Audio capture not found.");

            capture.IsDeleted = true;
            capture.DateUpdated = DateTimeOffset.UtcNow;
            await _audioCaptureRepository.SaveAsync(capture, ct);
            
            return true;
        }
    }
}

