using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IPromptTemplateService
    {
        Task<PromptTemplateDto> GetAsync(string promptTemplateId);
        Task<IEnumerable<PromptTemplateDto>> GetAllAsync();

    }
}