using System.Text.Json;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;

namespace MedInsights.Services.Mappers
{
    public static class ActivityEntryMapper
    {
        // Map from Entity -> DTO (read back to client)
        public static ActivityLogItemDto ToDto(ActivityItems entity)
        {
            return new ActivityLogItemDto
            {
              UserFirstName = entity.UserFirstName,
              UserLastName = entity.UserLastName,
              Type = entity.ItemType,
              Key = entity.ItemKey,
              Value = entity.NumericValue,
              Unit = entity.Unit  
            };
        }

        // Map from Create DTO -> Entity (server generates RowKey)
        public static ActivityItems ToEntity(ActivityLogItemDto dto)
        {
            return new ActivityItems
            {
              UserFirstName = dto.UserFirstName,
              UserLastName = dto.UserLastName,
              ItemType = dto.Type,
              ItemKey = dto.Key,
              NumericValue = dto.Value,
              Unit = dto.Unit
            };
        }
    }
}
