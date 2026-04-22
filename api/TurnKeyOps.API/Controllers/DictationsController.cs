// using MedInsights.Controllers;
// using MedInsights.Lib.Dtos;
// using MedInsights.Models;
// using MedInsights.Services.Interfaces;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;

// namespace MedInsights.API.Controllers
// {
//     [ApiController]
//     [Route("api/[controller]")]
//     [Authorize] // ensures only authenticated users can access
//     public class DictationsController : ApiControllerBase
//     {
//         private readonly IDictationService _dictationService;

//         public DictationsController(
//             IDictationService dictationService
//             ) : base()
//         {
//             _dictationService = dictationService;
//         }

//         //start dictation endpoints
//         [HttpPost("start")]
//         public async Task<IActionResult> StartDictation([FromBody] DictationDto dto, CancellationToken ct)
//         {
//             var dictation = await _dictationService.StartDictationAsync(dto.PatientId, ct);
            
//             // return CreatedResponse(nameof(StartDictation), new { id = dictation.Id }, dictation);

//             return OkResponse(dictation);
//         }

//         /// <summary>
//         /// Upload a new dictation (saves blob + record + queues transcription).
//         /// </summary>
//         [HttpPost("upload")]
//         [Consumes("multipart/form-data")]
//         [RequestSizeLimit(100_000_000)] // ~100MB
//         public async Task<IActionResult> UploadDictation([FromForm] DictationUploadRequest request, CancellationToken ct)
//         {
//             var file = request.File;
//             if (file == null || file.Length == 0)
//                 return BadRequest("Invalid file.");

//             await using var stream = file.OpenReadStream();

//             var dictation = await _dictationService.AddOrAttachDictationAsync(
//                 stream,
//                 request.DictationId,
//                 ct);
            
//             return OkResponse(dictation);
//         }

//         /// <summary>
//         /// Get all dictations for current user.
//         /// </summary>
//         [HttpGet]
//         public async Task<IActionResult> GetMyDictations()
//         {
//             var dictations = await _dictationService.GetMyDictationsAsync();
//             return OkResponse(dictations);
//         }

//         /// <summary>
//         /// Get a single dictation by ID.
//         /// </summary>
//         [HttpGet("{id:guid}")]
//         public async Task<IActionResult> GetById(Guid id)
//         {
//             var dictation = await _dictationService.GetAsync(id);
//             if (dictation == null)
//                 return NotFound();

//             return OkResponse(dictation);
//         }

//         /// <summary>
//         /// Update an existing dictation (e.g., status or text).
//         /// </summary>
//         [HttpPut("{id:guid}")]
//         public async Task<IActionResult> Update(Guid id, [FromBody] DictationDto dto)
//         {
//             if (id != dto.Id)
//                 return BadRequest("ID mismatch.");

//             var updated = await _dictationService.UpdateAsync(dto);
//             return OkResponse(updated);
//         }

//         /// <summary>
//         /// Soft delete a dictation.
//         /// </summary>
//         [HttpDelete("{id:guid}")]
//         public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
//         {
//             var result = await _dictationService.DeleteAsync(id, ct);
//             if (!result) return NotFound();

//             return DeletedResponse(result);
//         }
//     }
// }
