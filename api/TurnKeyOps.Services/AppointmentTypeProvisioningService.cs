using MedInsights.Lib;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Enums;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;

namespace MedInsights.Services
{
    public sealed class AppointmentTypeProvisioningService : IAppointmentTypeProvisioningService
    {
        private static readonly (string Name, AppointmentTypeLocation Location)[] DefaultSeeds =
        [
            ("Telehealth Visit", AppointmentTypeLocation.Remote),
            ("At Home Visit", AppointmentTypeLocation.Home),
            ("On Site Visit", AppointmentTypeLocation.Facility)
        ];

        private readonly IAppointmentTypeRepository _repository;

        public AppointmentTypeProvisioningService(IAppointmentTypeRepository repository)
        {
            _repository = repository;
        }

        public async Task EnsureTenantHasActiveAppointmentTypesAsync(Guid tenantId, CancellationToken ct = default)
        {
            if (tenantId == Guid.Empty)
                return;

            var all = (await _repository.GetByTenantAsync(tenantId, includeDeleted: true, ct))
                .OrderBy(x => x.DateUpdated ?? x.DateCreated)
                .ToList();

            if (all.Count == 0)
            {
                await CreateDefaultAppointmentTypesAsync(tenantId, ct);
                return;
            }

            var active = all.Where(x => !x.IsDeleted && x.IsActive).ToList();
            if (active.Count > 0)
                return;

            var latestDeleted = all
                .Where(x => x.IsDeleted)
                .OrderByDescending(x => x.DateUpdated ?? x.DateCreated)
                .FirstOrDefault();

            if (latestDeleted is not null)
            {
                latestDeleted.IsDeleted = false;
                latestDeleted.IsActive = true;
                latestDeleted.UpdatedBy = "system";
                latestDeleted.DateUpdated = DateTime.UtcNow;
                await _repository.SaveAsync(latestDeleted, ct);
                return;
            }

            var latestInactive = all
                .Where(x => !x.IsDeleted)
                .OrderByDescending(x => x.DateUpdated ?? x.DateCreated)
                .FirstOrDefault();

            if (latestInactive is not null)
            {
                latestInactive.IsActive = true;
                latestInactive.UpdatedBy = "system";
                latestInactive.DateUpdated = DateTime.UtcNow;
                await _repository.SaveAsync(latestInactive, ct);
                return;
            }

            await CreateDefaultAppointmentTypesAsync(tenantId, ct);
        }

        private async Task CreateDefaultAppointmentTypesAsync(Guid tenantId, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var partitionKey = EntityKeyPolicy.TenantPartition(tenantId);

            foreach (var seed in DefaultSeeds)
            {
                var entity = new AppointmentTypeDefinition
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    PartitionKey = partitionKey,
                    RowKey = string.Empty,
                    Name = seed.Name,
                    Location = seed.Location,
                    IsActive = true,
                    AverageTimeInMinutes = 30,
                    Data = null,
                    CreatedBy = "system",
                    DateCreated = now,
                    UpdatedBy = "system",
                    DateUpdated = now
                };

                entity.RowKey = EntityKeyPolicy.Row(entity.Id);
                await _repository.SaveAsync(entity, ct);
            }
        }
    }
}
