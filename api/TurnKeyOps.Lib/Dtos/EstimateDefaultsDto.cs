namespace TurnKeyOps.Lib.Dtos;

public class EstimateDefaultsDto
{
    public decimal ConcreteCostPerYard { get; set; }
    public decimal MinimumLoadFee { get; set; }
    public decimal ShortLoadFee { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal FuelSurcharge { get; set; }
    public decimal DefaultPumpFee { get; set; }
    public decimal AdditiveCost { get; set; }
    public decimal FiberMeshCost { get; set; }
    public decimal ColorCost { get; set; }
    public decimal SealerCost { get; set; }

    public decimal DemoCostRate { get; set; }
    public decimal ExcavationCostRate { get; set; }
    public decimal HaulOffFee { get; set; }
    public decimal BaseMaterialUnitCost { get; set; }
    public decimal CompactionCost { get; set; }
    public decimal VaporBarrierCost { get; set; }
    public decimal GradingCost { get; set; }
    public decimal AccessDifficultyEasyPercent { get; set; }
    public decimal AccessDifficultyModeratePercent { get; set; }
    public decimal AccessDifficultyHardPercent { get; set; }

    public decimal RebarCostPerFoot { get; set; }
    public decimal MeshCost { get; set; }
    public decimal ChairsCost { get; set; }
    public decimal DowelsCost { get; set; }
    public decimal AnchorBoltsCost { get; set; }

    public decimal FormMaterialCost { get; set; }
    public decimal FormComplexitySimpleMultiplier { get; set; }
    public decimal FormComplexityStandardMultiplier { get; set; }
    public decimal FormComplexityComplexMultiplier { get; set; }
    public decimal FormLaborHoursPerLinearFoot { get; set; }

    public decimal SawCutCost { get; set; }
    public decimal JointMaterialCost { get; set; }
    public decimal ExpansionJointCost { get; set; }
    public decimal CuringCompoundCost { get; set; }
    public decimal StampPatternCost { get; set; }
    public decimal DecorativePremium { get; set; }

    public decimal LaborRatePerHour { get; set; }
    public decimal OvertimeMultiplier { get; set; }
    public int DefaultCrewSize { get; set; }
    public decimal DemoHoursPer100SqFt { get; set; }
    public decimal PrepHoursPer100SqFt { get; set; }
    public decimal FormHoursPer100LinearFt { get; set; }
    public decimal ReinforcementHoursPer100SqFt { get; set; }
    public decimal PourHoursPer100SqFt { get; set; }
    public decimal FinishHoursPer100SqFt { get; set; }

    public decimal SkidSteerCost { get; set; }
    public decimal ExcavatorCost { get; set; }
    public decimal CompactorCost { get; set; }
    public decimal SawEquipmentCost { get; set; }
    public decimal PowerTrowelCost { get; set; }
    public decimal TrailerTruckCost { get; set; }
    public decimal GeneratorCost { get; set; }
    public decimal BuggyCost { get; set; }
    public decimal OtherEquipmentCost { get; set; }

    public decimal OverheadPercent { get; set; }
    public decimal ContingencyPercent { get; set; }
    public decimal ProfitPercent { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TravelCharge { get; set; }
    public decimal RushFee { get; set; }
    public decimal WeatherRiskAllowance { get; set; }
}
