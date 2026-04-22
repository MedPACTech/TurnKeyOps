using System;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public class PatientContextService : IPatientContextService
    {
        private readonly IPatientContextRepository _contextRepository;
        private readonly IUserContext _userContext;

        public PatientContextService(IPatientContextRepository contextRepository, IUserContext userContext)
        {
            _contextRepository = contextRepository;
            _userContext = userContext;
        }

        private string PartitionKeyForTenantUser()
        {
            return EntityKeyPolicy.TenantUserPartition(_userContext.TenantId, _userContext.UserId);
        }

        public async Task<PatientContextDto?> GetActiveAsync()
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();
            var pk = PartitionKeyForTenantUser();
            var contexts = await _contextRepository.GetActivePatientAsync(pk);

            var active = contexts
                .OrderByDescending(c => c.DateActivated)
                .FirstOrDefault();

            return active == null ? null : PatientContextMapper.ToDto(active);
        }

        public async Task<IEnumerable<PatientContextDto>> GetHistoryAsync()
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForTenantUser();
            var contexts = await _contextRepository.GetPatientsAsync(pk);
            var sorted = contexts
                .OrderByDescending(c => c.DateActivated)
                .Take(10)
                .ToList();

            return sorted.Select(PatientContextMapper.ToDto);
        }

        public async Task<PatientContextDto> AddAsync(PatientContextDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var entity = PatientContextMapper.ToEntity(dto);

            if (!Guid.TryParse(entity.PatientId, out _))
                throw new ArgumentException("Invalid PatientId format.", nameof(entity.PatientId));

            entity.PartitionKey = PartitionKeyForTenantUser();
            entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
            entity.RowKey = EntityKeyPolicy.Row(entity.Id);
            entity.DateActivated = DateTime.UtcNow;

            await _contextRepository.SaveAsync(entity);
            return PatientContextMapper.ToDto(entity);
        }

        public async Task<PatientContextDto> ActivateAsync(PatientDto patient)
        {
            var userPk = PartitionKeyForTenantUser();
            var patientRowKey = EntityKeyPolicy.Row(patient.Id);
            var entity = await _contextRepository.GetByPatientIdAsync(userPk, patientRowKey);

            if (entity != null)
            {
                entity.FirstName = patient.FirstName;
                entity.LastName = patient.LastName;
                entity.DateOfBirth = DateTime.SpecifyKind(patient.DateOfBirth.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
                entity.Gender = patient.Gender;
                entity.DateActivated = DateTime.UtcNow;
                await _contextRepository.SaveAsync(entity);
            }
            else
            {
                var contexts = await _contextRepository.GetPatientsAsync(userPk);
                var lastPatient = contexts.MinBy(c => c.DateActivated);
                if (lastPatient != null && contexts.Count() >= 10)
                {
                    lastPatient.IsDeleted = true;
                    await _contextRepository.SaveAsync(lastPatient);
                }

                entity = new PatientContext
                {
                    PartitionKey = userPk,
                    Id = Guid.NewGuid(),
                    PatientId = patientRowKey,
                    FirstName = patient.FirstName,
                    LastName = patient.LastName,
                    DateOfBirth = DateTime.SpecifyKind(patient.DateOfBirth.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
                    Gender = patient.Gender,
                    DateActivated = DateTime.UtcNow
                };

                entity.RowKey = EntityKeyPolicy.Row(entity.Id);
                await _contextRepository.SaveAsync(entity);
            }
            return PatientContextMapper.ToDto(entity);
        }

        public async Task DeleteAsync(PatientContextDto dto, CancellationToken ct)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForTenantUser();
            var rowKey = EntityKeyPolicy.Row(dto.Id);
            var entity = await _contextRepository.GetAsync(pk, rowKey)
                        ?? throw new KeyNotFoundException("Context not found.");

            entity.IsDeleted = true;
            await _contextRepository.SaveAsync(entity, ct);
        }
    }
}

