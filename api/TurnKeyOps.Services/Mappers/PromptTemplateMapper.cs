using System;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class PromptTemplateMapper
    {
        public static PromptTemplateDto ToDto(PromptTemplate entity)
        {
            return new PromptTemplateDto
            {
                Id = entity.PartitionKey,
                PromptTemplateId = entity.RowKey,
                Entity = entity.Entity,
                Action = entity.Action,
                PromptTemplateName = entity.PromptTemplateName,
                Prompt = entity.Prompt
            };
        }

        public static PromptTemplate ToEntity(PromptTemplateDto dto)
        {
            return new PromptTemplate
            {
                Id = Guid.Empty,
                PartitionKey = dto.Id,
                RowKey = dto.PromptTemplateId,
                Entity = dto.Entity,
                Action = dto.Action,
                PromptTemplateName = dto.PromptTemplateName,
                Prompt = dto.Prompt,
                IsDeleted = false
            };
        }
    }
}
