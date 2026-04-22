using System;
using MedInsights.Lib.Dtos;
using MedInsights.Lib;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public class PatientNoteService : IPatientNoteService
    {
        private readonly IPatientNoteRepository _repository;
        private readonly INoteTypeProfileService _noteTypeProfileService;
        private readonly IUserContext _userContext;

        public PatientNoteService(
            IPatientNoteRepository repository,
            INoteTypeProfileService noteTypeProfileService,
            IUserContext userContext)
        {
            _repository = repository;
            _noteTypeProfileService = noteTypeProfileService;
            _userContext = userContext;
        }

        private string PartitionKeyForPatient(Guid patientId) => EntityKeyPolicy.TenantPatientPartition(_userContext.TenantId, patientId);

        public async Task<PatientNoteDto?> GetAsync(Guid patientId, Guid id)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(patientId);
            var rowKey = EntityKeyPolicy.Row(id);
            var note = await _repository.GetAsync(pk, rowKey);

            return note == null ? null : PatientNoteMapper.ToDto(note);
        }

        public async Task<IEnumerable<PatientNoteDto>> GetByPatientIdAsync(Guid patientId)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(patientId);
            var notes = await _repository.GetByPatientIdAsync(pk);

            return notes.Select(PatientNoteMapper.ToDto);
        }

        public async Task<PatientNoteDto> AddAsync(PatientNoteDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            dto = await ResolveNoteTypeProfileAsync(dto);

            var entity = PatientNoteMapper.ToEntity(dto);
            entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
            entity.PartitionKey = PartitionKeyForPatient(dto.PatientId);
            entity.RowKey = EntityKeyPolicy.Row(entity.Id);
            entity.AuthorId = _userContext.UserId;

            var saved = await _repository.SaveAsync(entity);
            return PatientNoteMapper.ToDto(saved);
        }

        public async Task<PatientNoteDto> UpdateAsync(PatientNoteDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            dto = await ResolveNoteTypeProfileAsync(dto);

            var pk = PartitionKeyForPatient(dto.PatientId);
            var rowKey = EntityKeyPolicy.Row(dto.Id);

            var existing = await _repository.GetAsync(pk, rowKey)
                        ?? throw new KeyNotFoundException("Note not found.");

            existing.NoteBody = dto.NoteBody;
            existing.NoteTypeId = dto.NoteTypeId;
            existing.NoteTypeProfileId = dto.NoteTypeProfileId;
            existing.Category = dto.Category;
            existing.Visibility = dto.Visibility;
            existing.Tags = dto.Tags;

            var saved = await _repository.SaveAsync(existing);
            return PatientNoteMapper.ToDto(saved);
        }

        public async Task DeleteAsync(Guid id)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var existing = await _repository.GetByRowKeyAsync(EntityKeyPolicy.Row(id), CancellationToken.None)
                ?? throw new KeyNotFoundException("Note not found.");

            existing.IsDeleted = true;
            await _repository.SaveAsync(existing);
        }

        private async Task<PatientNoteDto> ResolveNoteTypeProfileAsync(PatientNoteDto dto)
        {
            if (dto.NoteTypeId is null || dto.NoteTypeId == Guid.Empty)
            {
                dto.NoteTypeId = null;
                dto.NoteTypeProfileId = null;
                return dto;
            }

            var profile = await _noteTypeProfileService.GetByNoteTypeIdAsync(dto.NoteTypeId.Value);
            if (profile is null)
                throw new InvalidOperationException($"No note type profile found for note type '{dto.NoteTypeId}'.");

            if (dto.NoteTypeProfileId.HasValue
                && dto.NoteTypeProfileId.Value != Guid.Empty
                && dto.NoteTypeProfileId.Value != profile.Id)
            {
                throw new InvalidOperationException("Provided noteTypeProfileId does not match the selected noteTypeId.");
            }

            dto.NoteTypeProfileId = profile.Id;
            return dto;
        }
    }
}
