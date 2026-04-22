using MedInsights.Lib.Dtos;
using MedInsights.Lib;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Enums;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public class PatientContactService : IPatientContactService
    {
        private readonly IPatientContactRepository _contactRepository;
        private readonly IUserContext _userContext;

        public PatientContactService(IPatientContactRepository contactRepository, IUserContext userContext)
        {
            _contactRepository = contactRepository;
            _userContext = userContext;
        }

        private string PartitionKeyForPatient(Guid patientId) => EntityKeyPolicy.TenantPatientPartition(_userContext.TenantId, patientId);

        public async Task<PatientContactDto?> GetAsync(Guid patientId, Guid contactId)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(patientId);
            var rowKey = EntityKeyPolicy.Row(contactId);
            var contact = await _contactRepository.GetAsync(pk, rowKey);

            return contact == null ? null : PatientContactMapper.ToDto(contact);
        }

        public async Task<IEnumerable<PatientContactDto>> GetByPatientAsync(Guid patientId)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(patientId);
            var contacts = await _contactRepository.GetByPatientAsync(pk);

            return contacts.Select(PatientContactMapper.ToDto);
        }

        public async Task<PatientContactDto> AddAsync(PatientContactDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();
            ValidateContact(dto);

            var pk = PartitionKeyForPatient(dto.PatientId);
            var contacts = (await _contactRepository.GetByPatientAsync(pk)).ToList();

            if (contacts.Count == 0 && dto.Relationship != PatientRelationship.Self)
                throw new ArgumentException("The first contact for a patient must be Self.");

            if (contacts.Any(c => c.Relationship == PatientRelationship.Self) && dto.Relationship == PatientRelationship.Self)
                throw new ArgumentException("Patient already has a Self contact.");

            if (!dto.IsPrimary && !contacts.Any(c => c.IsPrimary))
                throw new ArgumentException("A primary contact is required.");

            if (dto.IsPrimary)
                await DemoteExistingContactsAsync(contacts.Where(c => c.IsPrimary), c => c.IsPrimary = false);

            if (dto.IsSecondary)
                await DemoteExistingContactsAsync(contacts.Where(c => c.IsSecondary), c => c.IsSecondary = false);

            NormalizeContact(dto);
            var entity = PatientContactMapper.ToEntity(dto);
            entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
            entity.PartitionKey = pk;
            entity.RowKey = EntityKeyPolicy.Row(entity.Id);

            var saved = await _contactRepository.SaveAsync(entity);
            return PatientContactMapper.ToDto(saved);
        }

        public async Task<PatientContactDto> UpdateAsync(PatientContactDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();
            ValidateContact(dto);

            var pk = PartitionKeyForPatient(dto.PatientId);
            var rowKey = EntityKeyPolicy.Row(dto.Id);
            var existing = await _contactRepository.GetAsync(pk, rowKey)
                ?? throw new KeyNotFoundException("Contact not found.");
            var contacts = (await _contactRepository.GetByPatientAsync(pk)).ToList();

            if (existing.Relationship == PatientRelationship.Self && dto.Relationship != PatientRelationship.Self)
                throw new ArgumentException("Self contact relationship cannot be changed.");

            if (dto.Relationship == PatientRelationship.Self && contacts.Any(c => c.Id != existing.Id && c.Relationship == PatientRelationship.Self))
                throw new ArgumentException("Patient already has a Self contact.");

            if (!dto.IsPrimary && !contacts.Any(c => c.Id != existing.Id && c.IsPrimary))
                throw new ArgumentException("A primary contact is required.");

            if (dto.IsPrimary)
                await DemoteExistingContactsAsync(contacts.Where(c => c.Id != existing.Id && c.IsPrimary), c => c.IsPrimary = false);

            if (dto.IsSecondary)
                await DemoteExistingContactsAsync(contacts.Where(c => c.Id != existing.Id && c.IsSecondary), c => c.IsSecondary = false);

            NormalizeContact(dto);
            var entity = PatientContactMapper.ToEntity(dto);
            entity.Id = existing.Id == Guid.Empty ? dto.Id : existing.Id;
            entity.PartitionKey = existing.PartitionKey;
            entity.RowKey = existing.RowKey;
            entity.ETag = existing.ETag;
            entity.Timestamp = existing.Timestamp;

            var saved = await _contactRepository.SaveAsync(entity);
            return PatientContactMapper.ToDto(saved);
        }

        public async Task DeleteAsync(PatientContactDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(dto.PatientId);
            var rowKey = EntityKeyPolicy.Row(dto.Id);
            var existing = await _contactRepository.GetAsync(pk, rowKey)
                ?? throw new KeyNotFoundException("Contact not found.");

            if (existing.Relationship == PatientRelationship.Self)
                throw new InvalidOperationException("Self contact cannot be deleted.");

            if (existing.IsPrimary)
                throw new InvalidOperationException("Primary contact cannot be deleted until another contact is marked as primary.");

            existing.IsDeleted = true;
            await _contactRepository.SaveAsync(existing);
        }

        private async Task DemoteExistingContactsAsync(IEnumerable<PatientContact> contacts, Action<PatientContact> mutator)
        {
            foreach (var contact in contacts)
            {
                mutator(contact);
                await _contactRepository.SaveAsync(contact);
            }
        }

        private static void ValidateContact(PatientContactDto dto)
        {
            if (dto.IsPrimary && dto.IsSecondary)
                throw new ArgumentException("A contact cannot be both primary and secondary.");

            if (!Enum.IsDefined(dto.Relationship))
                throw new ArgumentException("A valid relationship is required.");

            if (!Enum.IsDefined(dto.ContactType))
                throw new ArgumentException("A valid contact type is required.");

            if (dto.Relationship == PatientRelationship.Other && string.IsNullOrWhiteSpace(dto.OtherRelationship))
                throw new ArgumentException("OtherRelationship is required when relationship is Other.");

            if (dto.Relationship != PatientRelationship.Other && !string.IsNullOrWhiteSpace(dto.OtherRelationship))
                throw new ArgumentException("OtherRelationship can only be provided when relationship is Other.");

            if (string.IsNullOrWhiteSpace(dto.FirstName))
                throw new ArgumentException("First name is required.");

            if (string.IsNullOrWhiteSpace(dto.LastName))
                throw new ArgumentException("Last name is required.");
        }

        private static void NormalizeContact(PatientContactDto dto)
        {
            dto.FirstName = dto.FirstName.Trim();
            dto.LastName = dto.LastName.Trim();
            dto.MiddleName = string.IsNullOrWhiteSpace(dto.MiddleName) ? null : dto.MiddleName.Trim();
            dto.OrganizationName = string.IsNullOrWhiteSpace(dto.OrganizationName) ? null : dto.OrganizationName.Trim();
            dto.PrimaryPhone = string.IsNullOrWhiteSpace(dto.PrimaryPhone) ? null : dto.PrimaryPhone.Trim();
            dto.SecondaryPhone = string.IsNullOrWhiteSpace(dto.SecondaryPhone) ? null : dto.SecondaryPhone.Trim();
            dto.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
            dto.OtherRelationship = string.IsNullOrWhiteSpace(dto.OtherRelationship) ? null : dto.OtherRelationship.Trim();

            if (dto.Relationship == PatientRelationship.Self)
                dto.ContactType = ContactType.Self;
        }
    }
}
