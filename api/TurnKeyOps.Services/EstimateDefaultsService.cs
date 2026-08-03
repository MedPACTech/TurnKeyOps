using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services.Interfaces;
using TurnKeyOps.Services.Mappers;

namespace TurnKeyOps.Services;

public class EstimateDefaultsService : IEstimateDefaultsService
{
    private const string DefaultsRowKey = "ESTIMATE-DEFAULTS";

    private readonly IEstimateDefaultsRepository _repository;
    private readonly IUserContext _userContext;

    public EstimateDefaultsService(IEstimateDefaultsRepository repository, IUserContext userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    public async Task<EstimateDefaultsDto> GetAsync(CancellationToken ct = default)
    {
        var existing = await _repository.GetAsync(PartitionKey(), DefaultsRowKey, ct);
        return existing is null || existing.IsDeleted ? CreateBaselineDefaults() : EstimateDefaultsMapper.ToDto(existing);
    }

    public async Task<EstimateDefaultsDto> UpsertAsync(EstimateDefaultsDto dto, CancellationToken ct = default)
    {
        Validate(dto);

        var existing = await _repository.GetAsync(PartitionKey(), DefaultsRowKey, ct) ?? new EstimateDefaultsProfile
        {
            Id = _userContext.TenantId,
            PartitionKey = PartitionKey(),
            RowKey = DefaultsRowKey,
            DateCreated = DateTime.UtcNow,
            IsDeleted = false
        };

        var entity = EstimateDefaultsMapper.ToEntity(dto, existing, PartitionKey(), DefaultsRowKey, _userContext.TenantId);
        entity.IsDeleted = false;
        entity.DateCreated = existing.DateCreated == default ? DateTime.UtcNow : existing.DateCreated;

        var saved = await _repository.SaveAsync(entity, ct);
        return EstimateDefaultsMapper.ToDto(saved);
    }

    private string PartitionKey() => RepositoryKeyHelper.ToTenantPartitionKey(_userContext.TenantId);

    private static void Validate(EstimateDefaultsDto dto)
    {
        if (dto.DefaultCrewSize < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dto.DefaultCrewSize),
                dto.DefaultCrewSize,
                "Default crew size must be at least one.");
        }

        foreach (var property in typeof(EstimateDefaultsDto).GetProperties())
        {
            if (property.PropertyType != typeof(decimal))
            {
                continue;
            }

            var value = (decimal)(property.GetValue(dto) ?? 0m);
            if (value < 0m)
            {
                throw new ArgumentOutOfRangeException(
                    property.Name,
                    value,
                    $"{property.Name} cannot be negative.");
            }
        }
    }

    private static EstimateDefaultsDto CreateBaselineDefaults() => new()
    {
        ConcreteCostPerYard = 165m,
        MinimumLoadFee = 250m,
        ShortLoadFee = 125m,
        DeliveryFee = 140m,
        FuelSurcharge = 35m,
        DefaultPumpFee = 650m,
        AdditiveCost = 28m,
        FiberMeshCost = 18m,
        ColorCost = 42m,
        SealerCost = 0.85m,

        DemoCostRate = 4.5m,
        ExcavationCostRate = 6.25m,
        HaulOffFee = 325m,
        BaseMaterialUnitCost = 48m,
        CompactionCost = 180m,
        VaporBarrierCost = 0.65m,
        GradingCost = 295m,
        AccessDifficultyEasyPercent = 0m,
        AccessDifficultyModeratePercent = 7.5m,
        AccessDifficultyHardPercent = 15m,

        RebarCostPerFoot = 1.35m,
        MeshCost = 0.72m,
        ChairsCost = 0.18m,
        DowelsCost = 2.1m,
        AnchorBoltsCost = 4.5m,

        FormMaterialCost = 2.4m,
        FormComplexitySimpleMultiplier = 1m,
        FormComplexityStandardMultiplier = 1.2m,
        FormComplexityComplexMultiplier = 1.45m,
        FormLaborHoursPerLinearFoot = 0.2m,

        SawCutCost = 1.75m,
        JointMaterialCost = 0.9m,
        ExpansionJointCost = 1.15m,
        CuringCompoundCost = 0.35m,
        StampPatternCost = 3.85m,
        DecorativePremium = 12m,

        LaborRatePerHour = 68m,
        OvertimeMultiplier = 1.5m,
        DefaultCrewSize = 4,
        DemoHoursPer100SqFt = 2.5m,
        PrepHoursPer100SqFt = 2m,
        FormHoursPer100LinearFt = 4.25m,
        ReinforcementHoursPer100SqFt = 1.5m,
        PourHoursPer100SqFt = 1.2m,
        FinishHoursPer100SqFt = 1.75m,

        SkidSteerCost = 225m,
        ExcavatorCost = 425m,
        CompactorCost = 90m,
        SawEquipmentCost = 75m,
        PowerTrowelCost = 120m,
        TrailerTruckCost = 180m,
        GeneratorCost = 65m,
        BuggyCost = 55m,
        OtherEquipmentCost = 0m,

        OverheadPercent = 12m,
        ContingencyPercent = 5m,
        ProfitPercent = 18m,
        TaxPercent = 7.5m,
        TravelCharge = 125m,
        RushFee = 250m,
        WeatherRiskAllowance = 150m
    };
}
