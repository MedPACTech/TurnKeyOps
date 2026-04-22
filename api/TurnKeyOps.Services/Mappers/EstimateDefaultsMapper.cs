using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;

namespace TurnKeyOps.Services.Mappers;

public static class EstimateDefaultsMapper
{
    public static EstimateDefaultsDto ToDto(EstimateDefaultsProfile entity) => new()
    {
        ConcreteCostPerYard = entity.ConcreteCostPerYard,
        MinimumLoadFee = entity.MinimumLoadFee,
        ShortLoadFee = entity.ShortLoadFee,
        DeliveryFee = entity.DeliveryFee,
        FuelSurcharge = entity.FuelSurcharge,
        DefaultPumpFee = entity.DefaultPumpFee,
        AdditiveCost = entity.AdditiveCost,
        FiberMeshCost = entity.FiberMeshCost,
        ColorCost = entity.ColorCost,
        SealerCost = entity.SealerCost,
        DemoCostRate = entity.DemoCostRate,
        ExcavationCostRate = entity.ExcavationCostRate,
        HaulOffFee = entity.HaulOffFee,
        BaseMaterialUnitCost = entity.BaseMaterialUnitCost,
        CompactionCost = entity.CompactionCost,
        VaporBarrierCost = entity.VaporBarrierCost,
        GradingCost = entity.GradingCost,
        AccessDifficultyEasyPercent = entity.AccessDifficultyEasyPercent,
        AccessDifficultyModeratePercent = entity.AccessDifficultyModeratePercent,
        AccessDifficultyHardPercent = entity.AccessDifficultyHardPercent,
        RebarCostPerFoot = entity.RebarCostPerFoot,
        MeshCost = entity.MeshCost,
        ChairsCost = entity.ChairsCost,
        DowelsCost = entity.DowelsCost,
        AnchorBoltsCost = entity.AnchorBoltsCost,
        FormMaterialCost = entity.FormMaterialCost,
        FormComplexitySimpleMultiplier = entity.FormComplexitySimpleMultiplier,
        FormComplexityStandardMultiplier = entity.FormComplexityStandardMultiplier,
        FormComplexityComplexMultiplier = entity.FormComplexityComplexMultiplier,
        FormLaborHoursPerLinearFoot = entity.FormLaborHoursPerLinearFoot,
        SawCutCost = entity.SawCutCost,
        JointMaterialCost = entity.JointMaterialCost,
        ExpansionJointCost = entity.ExpansionJointCost,
        CuringCompoundCost = entity.CuringCompoundCost,
        StampPatternCost = entity.StampPatternCost,
        DecorativePremium = entity.DecorativePremium,
        LaborRatePerHour = entity.LaborRatePerHour,
        OvertimeMultiplier = entity.OvertimeMultiplier,
        DefaultCrewSize = entity.DefaultCrewSize,
        DemoHoursPer100SqFt = entity.DemoHoursPer100SqFt,
        PrepHoursPer100SqFt = entity.PrepHoursPer100SqFt,
        FormHoursPer100LinearFt = entity.FormHoursPer100LinearFt,
        ReinforcementHoursPer100SqFt = entity.ReinforcementHoursPer100SqFt,
        PourHoursPer100SqFt = entity.PourHoursPer100SqFt,
        FinishHoursPer100SqFt = entity.FinishHoursPer100SqFt,
        SkidSteerCost = entity.SkidSteerCost,
        ExcavatorCost = entity.ExcavatorCost,
        CompactorCost = entity.CompactorCost,
        SawEquipmentCost = entity.SawEquipmentCost,
        PowerTrowelCost = entity.PowerTrowelCost,
        TrailerTruckCost = entity.TrailerTruckCost,
        GeneratorCost = entity.GeneratorCost,
        BuggyCost = entity.BuggyCost,
        OtherEquipmentCost = entity.OtherEquipmentCost,
        OverheadPercent = entity.OverheadPercent,
        ContingencyPercent = entity.ContingencyPercent,
        ProfitPercent = entity.ProfitPercent,
        TaxPercent = entity.TaxPercent,
        TravelCharge = entity.TravelCharge,
        RushFee = entity.RushFee,
        WeatherRiskAllowance = entity.WeatherRiskAllowance
    };

    public static EstimateDefaultsProfile ToEntity(
        EstimateDefaultsDto dto,
        EstimateDefaultsProfile target,
        string partitionKey,
        string rowKey,
        Guid tenantId)
    {
        target.Id = target.Id == Guid.Empty ? tenantId : target.Id;
        target.PartitionKey = partitionKey;
        target.RowKey = rowKey;
        target.ConcreteCostPerYard = dto.ConcreteCostPerYard;
        target.MinimumLoadFee = dto.MinimumLoadFee;
        target.ShortLoadFee = dto.ShortLoadFee;
        target.DeliveryFee = dto.DeliveryFee;
        target.FuelSurcharge = dto.FuelSurcharge;
        target.DefaultPumpFee = dto.DefaultPumpFee;
        target.AdditiveCost = dto.AdditiveCost;
        target.FiberMeshCost = dto.FiberMeshCost;
        target.ColorCost = dto.ColorCost;
        target.SealerCost = dto.SealerCost;
        target.DemoCostRate = dto.DemoCostRate;
        target.ExcavationCostRate = dto.ExcavationCostRate;
        target.HaulOffFee = dto.HaulOffFee;
        target.BaseMaterialUnitCost = dto.BaseMaterialUnitCost;
        target.CompactionCost = dto.CompactionCost;
        target.VaporBarrierCost = dto.VaporBarrierCost;
        target.GradingCost = dto.GradingCost;
        target.AccessDifficultyEasyPercent = dto.AccessDifficultyEasyPercent;
        target.AccessDifficultyModeratePercent = dto.AccessDifficultyModeratePercent;
        target.AccessDifficultyHardPercent = dto.AccessDifficultyHardPercent;
        target.RebarCostPerFoot = dto.RebarCostPerFoot;
        target.MeshCost = dto.MeshCost;
        target.ChairsCost = dto.ChairsCost;
        target.DowelsCost = dto.DowelsCost;
        target.AnchorBoltsCost = dto.AnchorBoltsCost;
        target.FormMaterialCost = dto.FormMaterialCost;
        target.FormComplexitySimpleMultiplier = dto.FormComplexitySimpleMultiplier;
        target.FormComplexityStandardMultiplier = dto.FormComplexityStandardMultiplier;
        target.FormComplexityComplexMultiplier = dto.FormComplexityComplexMultiplier;
        target.FormLaborHoursPerLinearFoot = dto.FormLaborHoursPerLinearFoot;
        target.SawCutCost = dto.SawCutCost;
        target.JointMaterialCost = dto.JointMaterialCost;
        target.ExpansionJointCost = dto.ExpansionJointCost;
        target.CuringCompoundCost = dto.CuringCompoundCost;
        target.StampPatternCost = dto.StampPatternCost;
        target.DecorativePremium = dto.DecorativePremium;
        target.LaborRatePerHour = dto.LaborRatePerHour;
        target.OvertimeMultiplier = dto.OvertimeMultiplier;
        target.DefaultCrewSize = dto.DefaultCrewSize;
        target.DemoHoursPer100SqFt = dto.DemoHoursPer100SqFt;
        target.PrepHoursPer100SqFt = dto.PrepHoursPer100SqFt;
        target.FormHoursPer100LinearFt = dto.FormHoursPer100LinearFt;
        target.ReinforcementHoursPer100SqFt = dto.ReinforcementHoursPer100SqFt;
        target.PourHoursPer100SqFt = dto.PourHoursPer100SqFt;
        target.FinishHoursPer100SqFt = dto.FinishHoursPer100SqFt;
        target.SkidSteerCost = dto.SkidSteerCost;
        target.ExcavatorCost = dto.ExcavatorCost;
        target.CompactorCost = dto.CompactorCost;
        target.SawEquipmentCost = dto.SawEquipmentCost;
        target.PowerTrowelCost = dto.PowerTrowelCost;
        target.TrailerTruckCost = dto.TrailerTruckCost;
        target.GeneratorCost = dto.GeneratorCost;
        target.BuggyCost = dto.BuggyCost;
        target.OtherEquipmentCost = dto.OtherEquipmentCost;
        target.OverheadPercent = dto.OverheadPercent;
        target.ContingencyPercent = dto.ContingencyPercent;
        target.ProfitPercent = dto.ProfitPercent;
        target.TaxPercent = dto.TaxPercent;
        target.TravelCharge = dto.TravelCharge;
        target.RushFee = dto.RushFee;
        target.WeatherRiskAllowance = dto.WeatherRiskAllowance;
        target.DateUpdated = DateTime.UtcNow;
        return target;
    }
}
