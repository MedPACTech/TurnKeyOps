using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Enums;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public class PatientVitalsService : IPatientVitalsService
    {
        private readonly IPatientVitalsRepository _repository;
        private readonly IUserContext _userContext;

        public PatientVitalsService(IPatientVitalsRepository repository, IUserContext userContext)
        {
            _repository = repository;
            _userContext = userContext;
        }

        private string PartitionKeyForPatient(Guid patientId) => EntityKeyPolicy.TenantPatientPartition(_userContext.TenantId, patientId);

        public async Task<PatientVitalsDto?> GetAsync(
            Guid patientId,
            Guid vitalsId,
            VitalsUnitSystem? unitSystem = null)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(patientId);
            var rowKey = EntityKeyPolicy.Row(vitalsId);
            var item = await _repository.GetAsync(pk, rowKey);

            if (item == null) return null;
            NormalizeStoredPairs(item);
            return PatientVitalsMapper.ToDto(item, ResolveUnitSystem(unitSystem));
        }

        public async Task<IEnumerable<PatientVitalsDto>> GetByPatientAsync(
            Guid patientId,
            VitalsUnitSystem? unitSystem = null)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(patientId);
            var items = await _repository.GetByPatientAsync(pk);
            var resolvedUnitSystem = ResolveUnitSystem(unitSystem);

            return items
                .OrderByDescending(x => x.DateRead)
                .Select(x =>
            {
                NormalizeStoredPairs(x);
                return PatientVitalsMapper.ToDto(x, resolvedUnitSystem);
            });
        }

        public async Task<PatientVitalsDto> AddAsync(PatientVitalsDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();
            ValidateRequiredFieldsForAdd(dto);

            var entity = PatientVitalsMapper.ToEntity(dto);
            entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
            entity.PartitionKey = PartitionKeyForPatient(dto.PatientId);
            entity.RowKey = EntityKeyPolicy.Row(entity.Id);
            ApplyCreateConversions(entity, dto);
            NormalizeStoredPairs(entity);

            var saved = await _repository.SaveAsync(entity);
            return PatientVitalsMapper.ToDto(saved, dto.UnitSystem);
        }

        public async Task<PatientVitalsDto> UpdateAsync(PatientVitalsDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();
            ValidateRequiredFieldsForUpdate(dto);

            var pk = PartitionKeyForPatient(dto.PatientId);
            var rowKey = EntityKeyPolicy.Row(dto.Id);
            var existing = await _repository.GetAsync(pk, rowKey)
                ?? throw new KeyNotFoundException("Patient vitals record not found.");

            EnforceVitalsUpdateRules(existing);

            ApplyUpdateWithDeltas(existing, dto);
            NormalizeStoredPairs(existing);

            var saved = await _repository.SaveAsync(existing);
            return PatientVitalsMapper.ToDto(saved, dto.UnitSystem);
        }

        public async Task DeleteAsync(PatientVitalsDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();
            if (dto.Id == Guid.Empty) throw new ArgumentException("Vitals Id is required.");
            if (dto.PatientId == Guid.Empty) throw new ArgumentException("PatientId is required.");

            var pk = PartitionKeyForPatient(dto.PatientId);
            var rowKey = EntityKeyPolicy.Row(dto.Id);
            var existing = await _repository.GetAsync(pk, rowKey)
                ?? throw new KeyNotFoundException("Patient vitals record not found.");

            if (existing.PatientEncounterId.HasValue)
                throw new InvalidOperationException("Vitals record linked to a signed encounter cannot be deleted.");

            existing.IsDeleted = true;
            await _repository.SaveAsync(existing);
        }

        private static void ValidateRequiredFieldsForAdd(PatientVitalsDto dto)
        {
            if (dto.PatientId == Guid.Empty) throw new ArgumentException("PatientId is required.");
            if (dto.DateRead == default) throw new ArgumentException("DateRead is required.");
        }

        private static void ValidateRequiredFieldsForUpdate(PatientVitalsDto dto)
        {
            if (dto.Id == Guid.Empty) throw new ArgumentException("Vitals Id is required.");
            if (dto.PatientId == Guid.Empty) throw new ArgumentException("PatientId is required.");
            if (dto.DateRead == default) throw new ArgumentException("DateRead is required.");
        }

        private static void ApplyCreateConversions(PatientVitals entity, PatientVitalsDto dto)
        {
            entity.PatientEncounterId = dto.PatientEncounterId;
            SetTemperaturePair(entity, dto.Temperature, dto.UnitSystem, isTmax: false);
            SetTemperaturePair(entity, dto.Tmax, dto.UnitSystem, isTmax: true);
            SetHeightPair(entity, dto.Height, dto.UnitSystem);
            SetWeightPair(entity, dto.Weight, dto.UnitSystem);
            entity.BMI = VitalsConversionTools.CalculateBmi(entity.WeightKilograms, entity.HeightCentimeters) ?? dto.BMI;
        }

        private static void ApplyUpdateWithDeltas(PatientVitals entity, PatientVitalsDto dto)
        {
            entity.PatientId = dto.PatientId;
            if (dto.PatientEncounterId.HasValue)
                entity.PatientEncounterId = dto.PatientEncounterId;
            entity.SystolicBloodPressure = dto.SystolicBloodPressure;
            entity.DiastolicBloodPressure = dto.DiastolicBloodPressure;
            entity.RespitoryRate = dto.RespitoryRate;
            entity.HeartRate = dto.HeartRate;
            entity.HeartRateQuality = dto.HeartRateQuality;
            entity.PulseOximetry = dto.PulseOximetry;
            entity.DateRead = dto.DateRead;

            UpdateTemperatureByDelta(entity, dto.Temperature, dto.UnitSystem, isTmax: false);
            UpdateTemperatureByDelta(entity, dto.Tmax, dto.UnitSystem, isTmax: true);
            UpdateHeightByDelta(entity, dto.Height, dto.UnitSystem);
            UpdateWeightByDelta(entity, dto.Weight, dto.UnitSystem);

            entity.BMI = VitalsConversionTools.CalculateBmi(entity.WeightKilograms, entity.HeightCentimeters) ?? dto.BMI ?? entity.BMI;
        }

        private static void UpdateTemperatureByDelta(PatientVitals entity, decimal? incoming, VitalsUnitSystem unitSystem, bool isTmax)
        {
            if (!incoming.HasValue) return;

            var current = isTmax
                ? (unitSystem == VitalsUnitSystem.Metric ? entity.TmaxCelsius : entity.TmaxFahrenheit)
                : (unitSystem == VitalsUnitSystem.Metric ? entity.TemperatureCelsius : entity.TemperatureFahrenheit);

            if (current.HasValue && current.Value == incoming.Value) return;
            SetTemperaturePair(entity, incoming, unitSystem, isTmax);
        }

        private static void UpdateHeightByDelta(PatientVitals entity, decimal? incoming, VitalsUnitSystem unitSystem)
        {
            if (!incoming.HasValue) return;

            var current = unitSystem == VitalsUnitSystem.Metric ? entity.HeightCentimeters : entity.HeightInches;
            if (current.HasValue && current.Value == incoming.Value) return;
            SetHeightPair(entity, incoming, unitSystem);
        }

        private static void UpdateWeightByDelta(PatientVitals entity, decimal? incoming, VitalsUnitSystem unitSystem)
        {
            if (!incoming.HasValue) return;

            var current = unitSystem == VitalsUnitSystem.Metric ? entity.WeightKilograms : entity.WeightPounds;
            if (current.HasValue && current.Value == incoming.Value) return;
            SetWeightPair(entity, incoming, unitSystem);
        }

        private static void SetTemperaturePair(PatientVitals entity, decimal? value, VitalsUnitSystem unitSystem, bool isTmax)
        {
            if (!value.HasValue)
            {
                if (isTmax)
                {
                    entity.TmaxCelsius = null;
                    entity.TmaxFahrenheit = null;
                }
                else
                {
                    entity.TemperatureCelsius = null;
                    entity.TemperatureFahrenheit = null;
                }
                return;
            }

            var metric = unitSystem == VitalsUnitSystem.Metric ? value.Value : VitalsConversionTools.FahrenheitToCelsius(value.Value);
            var imperial = unitSystem == VitalsUnitSystem.Imperial ? value.Value : VitalsConversionTools.CelsiusToFahrenheit(value.Value);

            if (isTmax)
            {
                entity.TmaxCelsius = metric;
                entity.TmaxFahrenheit = imperial;
            }
            else
            {
                entity.TemperatureCelsius = metric;
                entity.TemperatureFahrenheit = imperial;
            }
        }

        private static void SetHeightPair(PatientVitals entity, decimal? value, VitalsUnitSystem unitSystem)
        {
            if (!value.HasValue)
            {
                entity.HeightCentimeters = null;
                entity.HeightInches = null;
                return;
            }

            entity.HeightCentimeters = unitSystem == VitalsUnitSystem.Metric
                ? value.Value
                : VitalsConversionTools.InchesToCentimeters(value.Value);

            entity.HeightInches = unitSystem == VitalsUnitSystem.Imperial
                ? value.Value
                : VitalsConversionTools.CentimetersToInches(value.Value);
        }

        private static void SetWeightPair(PatientVitals entity, decimal? value, VitalsUnitSystem unitSystem)
        {
            if (!value.HasValue)
            {
                entity.WeightKilograms = null;
                entity.WeightPounds = null;
                return;
            }

            entity.WeightKilograms = unitSystem == VitalsUnitSystem.Metric
                ? value.Value
                : VitalsConversionTools.PoundsToKilograms(value.Value);

            entity.WeightPounds = unitSystem == VitalsUnitSystem.Imperial
                ? value.Value
                : VitalsConversionTools.KilogramsToPounds(value.Value);
        }

        private static void NormalizeStoredPairs(PatientVitals entity)
        {
            if (!entity.TemperatureCelsius.HasValue && entity.TemperatureFahrenheit.HasValue)
                entity.TemperatureCelsius = VitalsConversionTools.FahrenheitToCelsius(entity.TemperatureFahrenheit.Value);
            if (!entity.TemperatureFahrenheit.HasValue && entity.TemperatureCelsius.HasValue)
                entity.TemperatureFahrenheit = VitalsConversionTools.CelsiusToFahrenheit(entity.TemperatureCelsius.Value);

            if (!entity.TmaxCelsius.HasValue && entity.TmaxFahrenheit.HasValue)
                entity.TmaxCelsius = VitalsConversionTools.FahrenheitToCelsius(entity.TmaxFahrenheit.Value);
            if (!entity.TmaxFahrenheit.HasValue && entity.TmaxCelsius.HasValue)
                entity.TmaxFahrenheit = VitalsConversionTools.CelsiusToFahrenheit(entity.TmaxCelsius.Value);

            if (!entity.HeightCentimeters.HasValue && entity.HeightInches.HasValue)
                entity.HeightCentimeters = VitalsConversionTools.InchesToCentimeters(entity.HeightInches.Value);
            if (!entity.HeightInches.HasValue && entity.HeightCentimeters.HasValue)
                entity.HeightInches = VitalsConversionTools.CentimetersToInches(entity.HeightCentimeters.Value);

            if (!entity.WeightKilograms.HasValue && entity.WeightPounds.HasValue)
                entity.WeightKilograms = VitalsConversionTools.PoundsToKilograms(entity.WeightPounds.Value);
            if (!entity.WeightPounds.HasValue && entity.WeightKilograms.HasValue)
                entity.WeightPounds = VitalsConversionTools.KilogramsToPounds(entity.WeightKilograms.Value);
        }

        private static VitalsUnitSystem ResolveUnitSystem(VitalsUnitSystem? unitSystem)
            => unitSystem ?? VitalsUnitSystem.Imperial;

        private static void EnforceVitalsUpdateRules(PatientVitals existing)
        {
            if (existing.PatientEncounterId.HasValue)
                throw new InvalidOperationException("Vitals record is linked to a signed encounter and cannot be modified.");

            var dateReadUtc = ToUtc(existing.DateRead);
            if (DateTime.UtcNow - dateReadUtc >= TimeSpan.FromHours(24))
                throw new InvalidOperationException("Vitals record cannot be modified after 24 hours from DateRead.");
        }

        private static DateTime ToUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc) return value;
            if (value.Kind == DateTimeKind.Local) return value.ToUniversalTime();
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
    }
}
