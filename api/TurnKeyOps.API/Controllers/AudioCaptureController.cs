using MedInsights.Controllers;
using MedInsights.Lib.Dtos;
using MedInsights.Models;
using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedInsights.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // ensures only authenticated users can access
    public class AudioCapturesController : ApiControllerBase
    {
        private readonly IAudioCaptureService _audioCaptureService;

        public AudioCapturesController(IAudioCaptureService audioCaptureService
            ) : base()
        {
            _audioCaptureService = audioCaptureService;
        }

        //start dictation endpoints
        [HttpPost("start")]
        public async Task<IActionResult> StartCapture([FromBody] AudioCaptureDto dto, CancellationToken ct)
        {
            var capture = await _audioCaptureService.StartCaptureAsync(dto.PatientId, ct);
            
            // return CreatedResponse(nameof(StartCapture), new { id = capture.Id }, capture);
            return OkResponse(capture);
        }

        /// <summary>
        /// Upload a new dictation (saves blob + record + queues transcription).
        /// </summary>
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(100_000_000)] // ~100MB
        public async Task<IActionResult> UploadCapture([FromForm] AudioCaptureDto request, CancellationToken ct)
        {
            var file = request.File;
            if (file == null || file.Length == 0)
                return BadRequest("Invalid file.");

            await using var stream = file.OpenReadStream();

            var capture = await _audioCaptureService.AddOrAttachCaptureAsync(
                stream,
                request.Id,
                ct);
            
            return OkResponse(capture);
        }

        /// <summary>
        /// Get all captures for current user.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMyCaptures(CancellationToken ct)
        {
            var captures = await _audioCaptureService.GetMyCapturesAsync(ct);
            return OkResponse(captures);
        }

        /// <summary>
        /// Get a single capture by ID.
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            var capture = await _audioCaptureService.GetAsync(id, ct);
            if (capture == null)
                return NotFound();

            return OkResponse(capture);
        }

        /// <summary>
        /// Update an existing dictation (e.g., status or text).
        /// </summary>
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] AudioCaptureDto dto, CancellationToken ct)
        {
            if (id != dto.Id)
                return BadRequest("ID mismatch.");

            if (string.IsNullOrWhiteSpace(dto.JobToken))
                return BadRequest("JobToken is required.");

            try
            {
                // Special worker-only path: validate JobToken and update transcription
                // await _audioCaptureService.CompleteTranscriptionByIdAsync(
                //     id,
                //     dto.TranscribedText ?? string.Empty,
                //     dto.SpeechTokenCount,
                //     dto.JobToken,
                //     ct);

                var updated = await _audioCaptureService.UpdateAsync(dto, ct);
                return OkResponse(updated);

                //return OkResponse(new { success = true });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
        }

        /// <summary>
        /// Update an existing dictation (e.g., status or text).
        /// </summary>
        [AllowAnonymous]
        [HttpPost("/api/audiocaptures/transcription/{id:guid}")]
        public async Task<IActionResult> UpdateTranscription(Guid id, [FromBody] AudioCaptureTranscriptDto dto, CancellationToken ct)
        {
            if (id != dto.Id)
                return BadRequest("ID mismatch.");

            if (string.IsNullOrWhiteSpace(dto.JobToken))
                return BadRequest("JobToken is required.");

            try
            {
                var updated = await _audioCaptureService.UpdateTranscriptionAsync(dto, ct);
                return OkResponse(updated);

                //return OkResponse(new { success = true });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
        }

        /// <summary>
        /// Soft delete a dictation.
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            var result = await _audioCaptureService.DeleteAsync(id, ct);
            if (!result) return NotFound();

            return DeletedResponse(result);
        }
    }
}
