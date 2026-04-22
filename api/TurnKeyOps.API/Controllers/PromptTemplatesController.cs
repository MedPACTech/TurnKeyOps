using System.Security.Claims;
using Azure;
using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Services;
using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedInsights.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PromptTemplatesController : ApiControllerBase
    {
        private readonly IPromptTemplateService _service;

        public PromptTemplatesController(IPromptTemplateService service) : base()
        {
            _service = service;
        }

        // GET: api/prompt/{promptTemplateId}
        [HttpGet("{promptTemplateId}")]
        public async Task<IActionResult> GetAsync(string promptTemplateId)
        {

            var prompt = await _service.GetAsync(promptTemplateId);
            if (prompt == null)
            {
                return NotFound();
            }
            return OkResponse(prompt);
        }

        // GET All Prompts
        [HttpGet()]
        public async Task<IActionResult> GetAllPrompts()
        {
            var prompts = await _service.GetAllAsync();
            if (prompts == null || !prompts.Any())
            {
                return NotFound();
            }
            return OkResponse(prompts);
        }   
    }
}