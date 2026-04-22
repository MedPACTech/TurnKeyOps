using MedInsights.Lib.Dtos;
using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedInsights.Controllers
{
    [ApiController]
    [Route("api/note-types")]
    [Authorize]
    public class NoteTypesController : ApiControllerBase
    {
        private readonly INoteTypeService _service;

        public NoteTypesController(INoteTypeService service) : base()
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var noteTypes = await _service.GetAllAsync(ct);
            return OkResponse(noteTypes);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateNoteTypeDto dto, CancellationToken ct)
        {
            var created = await _service.CreateAsync(dto, ct);
            return OkResponse(created);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateNoteTypeDto dto, CancellationToken ct)
        {
            if (id != dto.Id)
                return BadRequest("ID mismatch.");

            var updated = await _service.UpdateAsync(id, dto, ct);
            return OkResponse(updated);
        }

        [HttpPut("{id:guid}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateNoteTypeStatusDto dto, CancellationToken ct)
        {
            var updated = await _service.UpdateStatusAsync(id, dto, ct);
            return OkResponse(updated);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            await _service.DeleteAsync(id, ct);
            return NoContent();
        }
    }
}
