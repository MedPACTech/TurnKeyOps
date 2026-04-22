using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class ActivityReadMapper
    {
        public static ActivityReadDto ToReadDto(ActivityItems e)
        {
            return new ActivityReadDto
            {
                TenantId = e.TenantId,
                UserId = e.UserId,
                EntryDate = e.EntryDate, // or e.EntryDate.Date if you want date-only

                UserFirstName = e.UserFirstName?.Trim() ?? string.Empty,
                UserLastName = e.UserLastName?.Trim() ?? string.Empty,

                Type = e.ItemType?.Trim().ToLowerInvariant() ?? string.Empty,
                Key = e.ItemKey?.Trim().ToLowerInvariant() ?? string.Empty,
                Value = e.NumericValue,
                Unit = e.Unit?.Trim().ToLowerInvariant()
            };
        }
    }
}
