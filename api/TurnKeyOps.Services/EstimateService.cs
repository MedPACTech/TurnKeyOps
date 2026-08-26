using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Enums;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services.Interfaces;
using TurnKeyOps.Services.Mappers;

namespace TurnKeyOps.Services;

public class EstimateService : IEstimateService
{
    private readonly IEstimateRepository _repo;
    private readonly IEstimateLineItemRepository _lineItemRepo;
    private readonly IEstimateTemplateRepository _templateRepo;
    private readonly IJobRepository _jobRepo;
    private readonly IEstimateDefaultsService _defaultsService;
    private readonly IEstimateWorkflowPayloadStore _payloadStore;
    private readonly IUserContext _userContext;

    public EstimateService(
        IEstimateRepository repo,
        IEstimateLineItemRepository lineItemRepo,
        IEstimateTemplateRepository templateRepo,
        IJobRepository jobRepo,
        IEstimateDefaultsService defaultsService,
        IEstimateWorkflowPayloadStore payloadStore,
        IUserContext userContext)
    {
        _repo = repo;
        _lineItemRepo = lineItemRepo;
        _templateRepo = templateRepo;
        _jobRepo = jobRepo;
        _defaultsService = defaultsService;
        _payloadStore = payloadStore;
        _userContext = userContext;
    }

    private string PartitionKeyForTenant() => RepositoryKeyHelper.ToTenantPartitionKey(_userContext.TenantId);

    public async Task<EstimateDto?> GetAsync(Guid id)
    {
        var entity = await GetEstimateEntityAsync(id);
        if (entity is null || entity.IsDeleted) return null;

        var liPk = RepositoryKeyHelper.ToTenantEstimatePartitionKey(_userContext.TenantId, id);
        var lineItems = (await _lineItemRepo.GetAllAsync(false, false)).Where(x => x.PartitionKey == liPk);
        var dto = EstimateMapper.ToDto(entity, lineItems.Where(li => !li.IsDeleted).OrderBy(li => li.SortOrder));
        await HydrateEstimateArtifactsAsync(entity, dto);
        return dto;
    }

    public async Task<(IEnumerable<EstimateDto> Items, string? ContinuationToken)> GetPagedAsync(int pageSize, string? continuationToken)
    {
        var pk = PartitionKeyForTenant();
        var offset = int.TryParse(continuationToken, out var parsed) ? parsed : 0;
        var all = (await _repo.GetAllAsync(false, false))
            .Where(x => x.PartitionKey == pk && !x.IsDeleted)
            .OrderByDescending(x => x.DateUpdated)
            .ToList();
        var items = all.Skip(offset).Take(pageSize).ToList();
        var token = offset + items.Count < all.Count ? (offset + items.Count).ToString() : null;
        return (items.Where(x => !x.IsDeleted).Select(x => EstimateMapper.ToDto(x)), token);
    }

    public async Task<EstimateDto> AddAsync(EstimateDto dto)
    {
        dto.Id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id;
        if (string.IsNullOrEmpty(dto.EstimateNumber))
            dto.EstimateNumber = $"EST-{DateTime.UtcNow:yyyyMMdd}-{dto.Id.ToString()[..4].ToUpper()}";

        ApplyFinancials(dto);

        var entity = EstimateMapper.ToEntity(dto, PartitionKeyForTenant());
        await PersistEstimateArtifactsAsync(entity, dto);
        await _repo.SaveAsync(entity);

        var liPk = RepositoryKeyHelper.ToTenantEstimatePartitionKey(_userContext.TenantId, dto.Id);
        foreach (var li in dto.LineItems)
        {
            li.EstimateId = dto.Id;
            var liEntity = EstimateLineItemMapper.ToEntity(li, liPk);
            await _lineItemRepo.SaveAsync(liEntity);
        }

        return await GetAsync(dto.Id) ?? EstimateMapper.ToDto(entity);
    }

    public async Task<EstimateDto> UpdateAsync(EstimateDto dto)
    {
        var existing = await GetEstimateEntityAsync(dto.Id)
            ?? throw new ArgumentException("Estimate not found", nameof(dto.Id));

        ApplyFinancials(dto);

        var entity = EstimateMapper.ToEntity(dto, existing.PartitionKey);
        entity.DateCreated = existing.DateCreated;
        await PersistEstimateArtifactsAsync(entity, dto);
        await _repo.SaveAsync(entity);

        return await GetAsync(dto.Id) ?? EstimateMapper.ToDto(entity);
    }

    public async Task<EstimateDto> CreateFromAppointmentAsync(CreateEstimateFromAppointmentRequestDto dto)
    {
        var structured = NormalizeStructuredInput(dto.StructuredInput);
        var snapshot = await CalculateAsync(structured);

        var estimate = new EstimateDto
        {
            Id = Guid.NewGuid(),
            EstimateNumber = string.IsNullOrWhiteSpace(dto.EstimateNumber)
                ? $"EST-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}"
                : dto.EstimateNumber,
            Status = EstimateStatus.Draft,
            TradeType = TradeType.Concrete,
            AppointmentId = dto.AppointmentId,
            CustomerId = Guid.Empty,
            CustomerName = dto.CustomerName,
            CustomerCompany = dto.CustomerCompany,
            ProjectAddress = dto.ProjectAddress,
            EstimatorName = dto.EstimatorName,
            ProjectName = dto.ProjectName,
            StructuredInput = structured,
            CalculationSnapshot = snapshot,
            TotalSqft = snapshot.SquareFeet,
            DepthInches = structured.DepthIn,
            CubicYards = snapshot.CubicYardsWithWaste,
            NumberOfPours = structured.PourCount,
            Subtotal = snapshot.FinalEstimatedPrice - snapshot.TaxAmount,
            TaxRate = (await _defaultsService.GetAsync()).TaxPercent / 100m,
            TaxAmount = snapshot.TaxAmount,
            Total = snapshot.FinalEstimatedPrice,
            Notes = "Created from mobile field workflow.",
            BobTranscript = dto.BobTranscript ?? new()
        };

        return await AddAsync(estimate);
    }

    public async Task<EstimateDto> UpdateStructuredAsync(Guid id, UpdateEstimateStructuredRequestDto dto)
    {
        var existing = await GetEstimateEntityAsync(id)
            ?? throw new ArgumentException("Estimate not found", nameof(id));

        var structured = NormalizeStructuredInput(dto.StructuredInput);
        var snapshot = await CalculateAsync(structured);
        var estimate = EstimateMapper.ToDto(existing);
        await HydrateEstimateArtifactsAsync(existing, estimate);

        estimate.AppointmentId = dto.AppointmentId;
        estimate.CustomerName = dto.CustomerName;
        estimate.CustomerCompany = dto.CustomerCompany;
        estimate.ProjectAddress = dto.ProjectAddress;
        estimate.EstimatorName = dto.EstimatorName;
        estimate.ProjectName = dto.ProjectName;
        estimate.StructuredInput = structured;
        estimate.CalculationSnapshot = snapshot;
        estimate.BobTranscript = dto.BobTranscript ?? estimate.BobTranscript;
        estimate.TotalSqft = snapshot.SquareFeet;
        estimate.DepthInches = structured.DepthIn;
        estimate.CubicYards = snapshot.CubicYardsWithWaste;
        estimate.NumberOfPours = structured.PourCount;
        estimate.Subtotal = snapshot.FinalEstimatedPrice - snapshot.TaxAmount;
        estimate.TaxRate = (await _defaultsService.GetAsync()).TaxPercent / 100m;
        estimate.TaxAmount = snapshot.TaxAmount;
        estimate.Total = snapshot.FinalEstimatedPrice;
        estimate.Status = estimate.Status == EstimateStatus.Submitted ? EstimateStatus.Submitted : EstimateStatus.Draft;

        var entity = EstimateMapper.ToEntity(estimate, existing.PartitionKey);
        entity.DateCreated = existing.DateCreated;
        await PersistEstimateArtifactsAsync(entity, estimate);
        await _repo.SaveAsync(entity);

        return await GetAsync(id) ?? EstimateMapper.ToDto(entity);
    }

    public async Task<EstimateCalculationSnapshotDto> CalculateAsync(StructuredEstimateInputDto dto)
    {
        var input = NormalizeStructuredInput(dto);
        var defaults = await _defaultsService.GetAsync();

        var squareFeet = Math.Max(0d, (input.LengthFt ?? 0d) * (input.WidthFt ?? 0d));
        var cubicFeet = squareFeet * ((input.DepthIn ?? 0d) / 12d);
        var cubicYards = cubicFeet / 27d;
        var wasteFactor = 1d + ((input.WastePercent ?? 10d) / 100d);
        var cubicYardsWithWaste = cubicYards * wasteFactor;
        var perimeterFeet = input.LengthFt.HasValue && input.WidthFt.HasValue
            ? (input.LengthFt.Value * 2d) + (input.WidthFt.Value * 2d)
            : 0d;

        var concreteMaterialCost = Round((decimal)cubicYardsWithWaste * defaults.ConcreteCostPerYard);
        if (string.Equals(input.ReinforcementType, "Fiber Mesh", StringComparison.OrdinalIgnoreCase))
        {
            concreteMaterialCost += defaults.FiberMeshCost;
        }

        var loadFee = cubicYardsWithWaste < 4d ? defaults.MinimumLoadFee : cubicYardsWithWaste < 8d ? defaults.ShortLoadFee : 0m;
        var deliveredConcreteCost = Round(concreteMaterialCost + loadFee + defaults.DeliveryFee + defaults.FuelSurcharge +
            ((input.PumpRequired ?? false) ? defaults.DefaultPumpFee : 0m));

        var baseSitePrep = 0m;
        if (input.DemoRequired ?? false)
        {
            baseSitePrep += Round((decimal)squareFeet * defaults.DemoCostRate);
            baseSitePrep += defaults.HaulOffFee;
        }
        if (input.ExcavationRequired ?? false)
        {
            baseSitePrep += Round((decimal)squareFeet * defaults.ExcavationCostRate);
            baseSitePrep += defaults.CompactionCost + defaults.GradingCost;
        }
        var siteAccessPercent = input.ExcavationRequired ?? false ? defaults.AccessDifficultyModeratePercent / 100m : 0m;
        var sitePrepSubtotal = Round(baseSitePrep + (baseSitePrep * siteAccessPercent));

        var reinforcementSubtotal = CalculateReinforcementSubtotal(input, defaults, squareFeet, perimeterFeet, cubicYardsWithWaste);
        var formworkSubtotal = Round(((decimal)perimeterFeet * defaults.FormMaterialCost) * defaults.FormComplexityStandardMultiplier);
        var finishSubtotal = CalculateFinishSubtotal(input, defaults, squareFeet, perimeterFeet);

        var demoHours = input.DemoRequired ?? false ? ((decimal)squareFeet / 100m) * defaults.DemoHoursPer100SqFt : 0m;
        var prepHours = input.ExcavationRequired ?? false ? ((decimal)squareFeet / 100m) * defaults.PrepHoursPer100SqFt : 0m;
        var formHours = ((decimal)perimeterFeet / 100m) * defaults.FormHoursPer100LinearFt;
        var reinforcementHours = !string.Equals(input.ReinforcementType, "None", StringComparison.OrdinalIgnoreCase)
            ? ((decimal)squareFeet / 100m) * defaults.ReinforcementHoursPer100SqFt
            : 0m;
        var pourHours = ((decimal)squareFeet / 100m) * defaults.PourHoursPer100SqFt * Math.Max(1, input.PourCount ?? 1);
        var finishHours = ((decimal)squareFeet / 100m) * defaults.FinishHoursPer100SqFt;
        var totalLaborHours = RoundHours(demoHours + prepHours + formHours + reinforcementHours + pourHours + finishHours);

        var regularLaborHours = Math.Min(totalLaborHours, 8m * defaults.DefaultCrewSize);
        var overtimeLaborHours = Math.Max(0m, totalLaborHours - regularLaborHours);
        var regularLaborCost = Round(regularLaborHours * defaults.LaborRatePerHour);
        var overtimeLaborCost = Round(overtimeLaborHours * defaults.LaborRatePerHour * defaults.OvertimeMultiplier);
        var laborSubtotal = Round(regularLaborCost + overtimeLaborCost);

        var equipmentSubtotal = 0m;
        if (input.ExcavationRequired ?? false)
        {
            equipmentSubtotal += defaults.ExcavatorCost + defaults.CompactorCost;
        }
        if (input.DemoRequired ?? false)
        {
            equipmentSubtotal += defaults.SkidSteerCost + defaults.TrailerTruckCost;
        }
        if (string.Equals(input.FinishType, "Smooth", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(input.FinishType, "Stamped", StringComparison.OrdinalIgnoreCase))
        {
            equipmentSubtotal += defaults.PowerTrowelCost;
        }
        if (perimeterFeet > 0)
        {
            equipmentSubtotal += defaults.SawEquipmentCost;
        }
        equipmentSubtotal = Round(equipmentSubtotal + defaults.OtherEquipmentCost);

        var directCost = Round(deliveredConcreteCost + sitePrepSubtotal + reinforcementSubtotal + formworkSubtotal + finishSubtotal + laborSubtotal + equipmentSubtotal);
        var overheadAmount = Round(directCost * (defaults.OverheadPercent / 100m));
        var contingencyAmount = Round(directCost * (defaults.ContingencyPercent / 100m));
        var profitAmount = Round(directCost * (defaults.ProfitPercent / 100m));
        var taxableBase = directCost + overheadAmount + contingencyAmount + profitAmount + defaults.TravelCharge + defaults.RushFee + defaults.WeatherRiskAllowance;
        var taxAmount = Round(taxableBase * (defaults.TaxPercent / 100m));
        var finalEstimatedPrice = Round(taxableBase + taxAmount);
        var pricePerSquareFoot = squareFeet > 0 ? Round(finalEstimatedPrice / (decimal)squareFeet) : 0m;
        var pricePerYard = cubicYardsWithWaste > 0 ? Round(finalEstimatedPrice / (decimal)cubicYardsWithWaste) : 0m;

        return new EstimateCalculationSnapshotDto
        {
            SquareFeet = RoundDouble(squareFeet),
            CubicFeet = RoundDouble(cubicFeet),
            CubicYards = RoundDouble(cubicYards),
            CubicYardsWithWaste = RoundDouble(cubicYardsWithWaste),
            ConcreteMaterialCost = concreteMaterialCost,
            DeliveredConcreteCost = deliveredConcreteCost,
            SitePrepSubtotal = sitePrepSubtotal,
            ReinforcementSubtotal = reinforcementSubtotal,
            FormworkSubtotal = formworkSubtotal,
            FinishSubtotal = finishSubtotal,
            TotalLaborHours = totalLaborHours,
            RegularLaborCost = regularLaborCost,
            OvertimeLaborCost = overtimeLaborCost,
            LaborSubtotal = laborSubtotal,
            EquipmentSubtotal = equipmentSubtotal,
            DirectCost = directCost,
            OverheadAmount = overheadAmount,
            ContingencyAmount = contingencyAmount,
            ProfitAmount = profitAmount,
            TaxAmount = taxAmount,
            FinalEstimatedPrice = finalEstimatedPrice,
            PricePerSquareFoot = pricePerSquareFoot,
            PricePerYard = pricePerYard
        };
    }

    public async Task<EstimateDto> SubmitAsync(Guid id)
    {
        var (existing, estimate) = await LoadEstimateForWorkflow(id);
        estimate.StructuredInput ??= new StructuredEstimateInputDto();
        estimate.CalculationSnapshot = await CalculateAsync(estimate.StructuredInput);
        estimate.Status = EstimateStatus.Submitted;
        estimate.SubmittedDate = DateTime.UtcNow;
        estimate.TotalSqft = estimate.CalculationSnapshot.SquareFeet;
        estimate.CubicYards = estimate.CalculationSnapshot.CubicYardsWithWaste;
        estimate.DepthInches = estimate.StructuredInput.DepthIn;
        estimate.NumberOfPours = estimate.StructuredInput.PourCount;
        estimate.Subtotal = estimate.CalculationSnapshot.FinalEstimatedPrice - estimate.CalculationSnapshot.TaxAmount;
        estimate.TaxAmount = estimate.CalculationSnapshot.TaxAmount;
        estimate.Total = estimate.CalculationSnapshot.FinalEstimatedPrice;
        estimate.TaxRate = (await _defaultsService.GetAsync()).TaxPercent / 100m;

        var entity = EstimateMapper.ToEntity(estimate, existing.PartitionKey);
        entity.DateCreated = existing.DateCreated;
        await PersistEstimateArtifactsAsync(entity, estimate);
        await _repo.SaveAsync(entity);

        return await GetAsync(id) ?? EstimateMapper.ToDto(entity);
    }

    public async Task<EstimateDto> StartReviewAsync(Guid id)
    {
        var (existing, estimate) = await LoadEstimateForWorkflow(id);
        EnsureStatus(estimate.Status, EstimateStatus.Submitted, EstimateStatus.Revised);
        estimate.Status = EstimateStatus.UnderReview;
        return await SaveWorkflowEstimate(existing, estimate);
    }

    public async Task<EstimateDto> AwardAsync(Guid id)
    {
        var (existing, estimate) = await LoadEstimateForWorkflow(id);
        EnsureStatus(estimate.Status, EstimateStatus.Submitted, EstimateStatus.UnderReview, EstimateStatus.Revised);
        estimate.Status = EstimateStatus.Awarded;
        estimate.AwardedDate = DateTime.UtcNow;
        return await SaveWorkflowEstimate(existing, estimate);
    }

    public async Task<EstimateDto> RejectAsync(Guid id)
    {
        var (existing, estimate) = await LoadEstimateForWorkflow(id);
        EnsureStatus(
            estimate.Status,
            EstimateStatus.Submitted,
            EstimateStatus.UnderReview,
            EstimateStatus.Revised,
            EstimateStatus.Awarded);
        estimate.Status = EstimateStatus.Rejected;
        estimate.RejectedDate = DateTime.UtcNow;
        return await SaveWorkflowEstimate(existing, estimate);
    }

    public async Task<EstimateDto> ReviseAsync(Guid id)
    {
        var (existing, estimate) = await LoadEstimateForWorkflow(id);
        EnsureStatus(estimate.Status, EstimateStatus.Submitted, EstimateStatus.UnderReview, EstimateStatus.Rejected);
        estimate.Status = EstimateStatus.Revised;
        estimate.RevisedDate = DateTime.UtcNow;
        return await SaveWorkflowEstimate(existing, estimate);
    }

    public async Task<JobDto> ConvertToJobAsync(Guid id)
    {
        var (existing, estimate) = await LoadEstimateForWorkflow(id);
        EnsureStatus(estimate.Status, EstimateStatus.Awarded);

        estimate.CalculationSnapshot ??= await CalculateAsync(estimate.StructuredInput ?? new StructuredEstimateInputDto());

        var job = new JobDto
        {
            Id = Guid.NewGuid(),
            Name = !string.IsNullOrWhiteSpace(estimate.ProjectName)
                ? estimate.ProjectName!
                : !string.IsNullOrWhiteSpace(estimate.CustomerName)
                    ? $"{estimate.CustomerName} Concrete Job"
                    : $"Job from {estimate.EstimateNumber}",
            Description = $"Converted from estimate {estimate.EstimateNumber}",
            TradeType = estimate.TradeType,
            Status = JobStatus.Created,
            CustomerId = estimate.CustomerId,
            CustomerName = estimate.CustomerName,
            EstimateId = estimate.Id,
            EstimateNumber = estimate.EstimateNumber,
            ProjectAddress = estimate.ProjectAddress,
            ProjectName = estimate.ProjectName,
            EstimateSnapshot = estimate.CalculationSnapshot,
            EstimatedTotal = estimate.CalculationSnapshot.FinalEstimatedPrice,
            Notes = estimate.Notes
        };

        var jobEntity = JobMapper.ToEntity(job, PartitionKeyForTenant());
        await PersistJobArtifactsAsync(jobEntity, job);
        await _jobRepo.SaveAsync(jobEntity);

        estimate.Status = EstimateStatus.ConvertedToJob;
        estimate.JobId = job.Id;
        estimate.JobName = job.Name;
        estimate.ConvertedJobId = job.Id;
        estimate.ConvertedToJobDate = DateTime.UtcNow;
        await SaveWorkflowEstimate(existing, estimate);

        return JobMapper.ToDto(jobEntity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetEstimateEntityAsync(id);
        if (entity is null) return;
        entity.IsDeleted = true;
        entity.DateUpdated = DateTime.UtcNow;
        await _repo.SaveAsync(entity);
    }

    public async Task<EstimateDto> CreateFromTemplateAsync(Guid templateId, Guid customerId, Guid? jobId)
    {
        var template = await _templateRepo.GetByIdAsync(templateId)
            ?? throw new ArgumentException("Template not found", nameof(templateId));

        var dto = new EstimateDto
        {
            TradeType = template.TradeType,
            CustomerId = customerId,
            JobId = jobId,
            Status = EstimateStatus.Draft,
            Notes = $"Created from template: {template.Name}"
        };

        if (!string.IsNullOrEmpty(template.DefaultLineItemsJson))
        {
            dto.LineItems = System.Text.Json.JsonSerializer.Deserialize<List<EstimateLineItemDto>>(
                template.DefaultLineItemsJson) ?? new();
        }

        return await AddAsync(dto);
    }

    public Task<ConcreteCalculatorResult> CalculateConcreteAsync(ConcreteCalculatorRequest request)
    {
        var sqft = request.LengthFeet * request.WidthFeet;
        var cy = ConcreteEstimator.CalculateCubicYards(sqft, request.DepthInches, request.WastePercent);
        var rebar = ConcreteEstimator.EstimateRebarLinearFeet(sqft);
        var forms = ConcreteEstimator.EstimateFormBoardLinearFeet(sqft);

        var mixPrice = request.ReadyMixPricePerCy ?? 165m;
        var laborPrice = request.LaborPricePerSqft ?? 4m;

        var materialCost = (decimal)cy * mixPrice + (decimal)rebar * 1.50m;
        var laborCost = (decimal)sqft * laborPrice;

        return Task.FromResult(new ConcreteCalculatorResult
        {
            Sqft = sqft,
            DepthInches = request.DepthInches,
            CubicYards = cy,
            CubicYardsPerPour = request.NumberOfPours > 0 ? cy / request.NumberOfPours : cy,
            RebarLinearFeet = rebar,
            FormBoardLinearFeet = forms,
            EstimatedMaterialCost = Math.Round(materialCost, 2),
            EstimatedLaborCost = Math.Round(laborCost, 2),
            EstimatedTotal = Math.Round(materialCost + laborCost, 2),
            NumberOfPours = request.NumberOfPours
        });
    }

    public async Task<EstimateDto> SignAsync(Guid estimateId, string signatureDataUrl, string signedByName)
    {
        var entity = await GetEstimateEntityAsync(estimateId)
            ?? throw new ArgumentException("Estimate not found", nameof(estimateId));

        entity.SignatureDataUrl = signatureDataUrl;
        entity.SignedByName = signedByName;
        entity.SignedDate = DateTime.UtcNow;
        entity.Status = EstimateStatus.Accepted;
        entity.AcceptedDate = DateTime.UtcNow;
        entity.DateUpdated = DateTime.UtcNow;
        await _repo.SaveAsync(entity);

        return (await GetAsync(estimateId))!;
    }

    private static StructuredEstimateInputDto NormalizeStructuredInput(StructuredEstimateInputDto dto) => new()
    {
        ProjectType = dto.ProjectType?.Trim(),
        LengthFt = dto.LengthFt,
        WidthFt = dto.WidthFt,
        DepthIn = dto.DepthIn,
        WastePercent = dto.WastePercent is null or <= 0 ? 10d : dto.WastePercent,
        PourCount = dto.PourCount is null or <= 0 ? 1 : dto.PourCount,
        DemoRequired = dto.DemoRequired ?? false,
        ExcavationRequired = dto.ExcavationRequired ?? false,
        PumpRequired = dto.PumpRequired ?? false,
        ReinforcementType = string.IsNullOrWhiteSpace(dto.ReinforcementType) ? "None" : dto.ReinforcementType,
        FinishType = string.IsNullOrWhiteSpace(dto.FinishType) ? "Broom" : dto.FinishType
    };

    private static decimal CalculateReinforcementSubtotal(
        StructuredEstimateInputDto input,
        EstimateDefaultsDto defaults,
        double squareFeet,
        double perimeterFeet,
        double cubicYardsWithWaste)
    {
        if (string.Equals(input.ReinforcementType, "Rebar", StringComparison.OrdinalIgnoreCase))
        {
            return Round(((decimal)perimeterFeet * 2m * defaults.RebarCostPerFoot) + defaults.ChairsCost + defaults.DowelsCost + defaults.AnchorBoltsCost);
        }

        if (string.Equals(input.ReinforcementType, "Wire Mesh", StringComparison.OrdinalIgnoreCase))
        {
            return Round(((decimal)squareFeet * defaults.MeshCost) + defaults.ChairsCost);
        }

        if (string.Equals(input.ReinforcementType, "Fiber Mesh", StringComparison.OrdinalIgnoreCase))
        {
            return Round(((decimal)cubicYardsWithWaste * defaults.FiberMeshCost));
        }

        return 0m;
    }

    private static decimal CalculateFinishSubtotal(
        StructuredEstimateInputDto input,
        EstimateDefaultsDto defaults,
        double squareFeet,
        double perimeterFeet)
    {
        var subtotal = defaults.CuringCompoundCost;
        if (perimeterFeet > 0)
        {
            subtotal += Round((decimal)perimeterFeet * defaults.SawCutCost);
            subtotal += defaults.JointMaterialCost + defaults.ExpansionJointCost;
        }

        if (string.Equals(input.FinishType, "Stamped", StringComparison.OrdinalIgnoreCase))
        {
            subtotal += defaults.StampPatternCost + defaults.DecorativePremium;
        }
        else if (string.Equals(input.FinishType, "Exposed Aggregate", StringComparison.OrdinalIgnoreCase))
        {
            subtotal += defaults.DecorativePremium;
        }

        return Round(subtotal + (decimal)squareFeet * defaults.SealerCost);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static decimal RoundHours(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static double RoundDouble(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private async Task<(Estimate Entity, EstimateDto Dto)> LoadEstimateForWorkflow(Guid id)
    {
        var existing = await GetEstimateEntityAsync(id)
            ?? throw new ArgumentException("Estimate not found", nameof(id));
        var dto = EstimateMapper.ToDto(existing);
        await HydrateEstimateArtifactsAsync(existing, dto);
        return (existing, dto);
    }

    private async Task<EstimateDto> SaveWorkflowEstimate(Estimate existing, EstimateDto estimate)
    {
        var entity = EstimateMapper.ToEntity(estimate, existing.PartitionKey);
        entity.DateCreated = existing.DateCreated;
        await PersistEstimateArtifactsAsync(entity, estimate);
        await _repo.SaveAsync(entity);
        return await GetAsync(estimate.Id) ?? EstimateMapper.ToDto(entity);
    }

    private async Task PersistEstimateArtifactsAsync(Estimate entity, EstimateDto dto)
    {
        entity.StructuredInputBlobName = await _payloadStore.SaveEstimateStructuredInputAsync(_userContext.TenantId, dto.Id, dto.StructuredInput);
        entity.CalculationSnapshotBlobName = await _payloadStore.SaveEstimateCalculationSnapshotAsync(_userContext.TenantId, dto.Id, dto.CalculationSnapshot);
        entity.BobTranscriptBlobName = await _payloadStore.SaveEstimateTranscriptAsync(_userContext.TenantId, dto.Id, dto.BobTranscript);
        entity.StructuredInputJson = null;
        entity.CalculationSnapshotJson = null;
    }

    private async Task PersistJobArtifactsAsync(Job entity, JobDto dto)
    {
        entity.EstimateSnapshotBlobName = await _payloadStore.SaveJobEstimateSnapshotAsync(_userContext.TenantId, dto.Id, dto.EstimateSnapshot);
        entity.EstimateSnapshotJson = null;
    }

    private async Task HydrateEstimateArtifactsAsync(Estimate entity, EstimateDto dto)
    {
        dto.StructuredInput = await _payloadStore.LoadEstimateStructuredInputAsync(entity.StructuredInputBlobName, entity.StructuredInputJson);
        dto.CalculationSnapshot = await _payloadStore.LoadEstimateCalculationSnapshotAsync(entity.CalculationSnapshotBlobName, entity.CalculationSnapshotJson);
        dto.BobTranscript = (await _payloadStore.LoadEstimateTranscriptAsync(entity.BobTranscriptBlobName)).ToList();
    }

    private static void EnsureStatus(EstimateStatus current, params EstimateStatus[] allowed)
    {
        if (allowed.Contains(current))
        {
            return;
        }

        throw new InvalidOperationException($"Estimate status {current} cannot perform this action.");
    }

    private static void ApplyFinancials(EstimateDto dto)
    {
        if (dto.LineItems.Count > 0 || dto.CalculationSnapshot is null)
        {
            dto.Subtotal = dto.LineItems.Sum(li => li.LineTotal);
            dto.TaxAmount = Math.Round(dto.Subtotal * dto.TaxRate, 2);
            dto.Total = dto.Subtotal + dto.TaxAmount;
            return;
        }

        dto.Subtotal = dto.CalculationSnapshot.FinalEstimatedPrice - dto.CalculationSnapshot.TaxAmount;
        dto.TaxAmount = dto.CalculationSnapshot.TaxAmount;
        dto.Total = dto.CalculationSnapshot.FinalEstimatedPrice;
    }

    private Task<Estimate?> GetEstimateEntityAsync(Guid id) =>
        _repo.GetAsync(PartitionKeyForTenant(), RepositoryKeyHelper.ToRowKey(id));
}
