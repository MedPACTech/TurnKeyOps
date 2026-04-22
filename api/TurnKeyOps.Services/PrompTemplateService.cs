using System;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Repositories;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public class PromptTemplateService : IPromptTemplateService
    {
        private readonly IPromptTemplateRepository _repository;

        public PromptTemplateService(IPromptTemplateRepository repository)
        {
            _repository = repository;
        }

        public async Task<PromptTemplateDto> GetAsync(string promptTemplateId)
        {
            var pk = "PromptTemplate";
            var rk = promptTemplateId;

            var prompt = await _repository.GetAsync(pk, rk, CancellationToken.None);
            return PromptTemplateMapper.ToDto(prompt);
        }

        public async Task<IEnumerable<PromptTemplateDto>> GetAllAsync()
        {
            var pk = "PromptTemplate";
            var prompts = await _repository.GetAllAsync(pk);
            return prompts.Select(p => PromptTemplateMapper.ToDto(p)).ToList();
        }
    }
}