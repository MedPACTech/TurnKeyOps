using MedInsights.Lib.Dtos;
using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedInsights.Controllers
{
    [ApiController]
    [Route("api/note-type-profiles")]
    [Authorize]
    public class NoteTypeProfilesController : ApiControllerBase
    {
        private readonly INoteTypeProfileService _service;

        public NoteTypeProfilesController(INoteTypeProfileService service) : base()
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var profiles = await _service.GetAllAsync(ct);
            return OkResponse(profiles);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var profile = await _service.GetAsync(id, ct);
            if (profile is null)
                return NotFound();

            return OkResponse(profile);
        }

        [HttpGet("by-note-type/{noteTypeId:guid}")]
        public async Task<IActionResult> GetByNoteTypeId(Guid noteTypeId, CancellationToken ct)
        {
            var profile = await _service.GetByNoteTypeIdAsync(noteTypeId, ct);
            if (profile is null)
                return NotFound();

            return OkResponse(profile);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateNoteTypeProfileDto dto, CancellationToken ct)
        {
            var created = await _service.CreateAsync(dto, ct);
            return OkResponse(created);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateNoteTypeProfileDto dto, CancellationToken ct)
        {
            if (id != dto.Id)
                return BadRequest("ID mismatch.");

            var updated = await _service.UpdateAsync(id, dto, ct);
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
