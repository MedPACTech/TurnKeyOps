using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Enums;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services.Interfaces;
using TurnKeyOps.Services.Mappers;

namespace TurnKeyOps.Services;

public class InvoiceService : IInvoiceService
{
    private readonly IInvoiceRepository _repo;
    private readonly IEstimateRepository _estimateRepo;
    private readonly IEstimateLineItemRepository _estimateLineItemRepo;
    private readonly IUserContext _userContext;

    public InvoiceService(
        IInvoiceRepository repo,
        IEstimateRepository estimateRepo,
        IEstimateLineItemRepository estimateLineItemRepo,
        IUserContext userContext)
    {
        _repo = repo;
        _estimateRepo = estimateRepo;
        _estimateLineItemRepo = estimateLineItemRepo;
        _userContext = userContext;
    }

    private string PartitionKeyForTenant() => RepositoryKeyHelper.ToTenantPartitionKey(_userContext.TenantId);

    public async Task<InvoiceDto?> GetAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        return entity is null || entity.IsDeleted ? null : InvoiceMapper.ToDto(entity);
    }

    public async Task<(IEnumerable<InvoiceDto> Items, string? ContinuationToken)> GetPagedAsync(int pageSize, string? continuationToken)
    {
        var pk = PartitionKeyForTenant();
        var offset = int.TryParse(continuationToken, out var parsed) ? parsed : 0;
        var all = (await _repo.GetAllAsync(false, false))
            .Where(x => x.PartitionKey == pk && !x.IsDeleted)
            .OrderByDescending(x => x.DateUpdated)
            .ToList();
        var items = all.Skip(offset).Take(pageSize).ToList();
        var token = offset + items.Count < all.Count ? (offset + items.Count).ToString() : null;
        return (items.Where(x => !x.IsDeleted).Select(InvoiceMapper.ToDto), token);
    }

    public async Task<InvoiceDto> AddAsync(InvoiceDto dto)
    {
        dto.Id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id;
        if (string.IsNullOrEmpty(dto.InvoiceNumber))
            dto.InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{dto.Id.ToString()[..4].ToUpper()}";

        dto.BalanceDue = dto.Total - dto.AmountPaid;

        var entity = InvoiceMapper.ToEntity(dto, PartitionKeyForTenant());
        await _repo.SaveAsync(entity);
        return InvoiceMapper.ToDto(entity);
    }

    public async Task<InvoiceDto> UpdateAsync(InvoiceDto dto)
    {
        var existing = await _repo.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException("Invoice not found", nameof(dto.Id));
        var entity = InvoiceMapper.ToEntity(dto, existing.PartitionKey);
        entity.DateCreated = existing.DateCreated;
        await _repo.SaveAsync(entity);
        return InvoiceMapper.ToDto(entity);
    }

    public async Task<InvoiceDto> CreateFromEstimateAsync(Guid estimateId)
    {
        var estimate = await _estimateRepo.GetByIdAsync(estimateId)
            ?? throw new ArgumentException("Estimate not found", nameof(estimateId));

        var invoiceDto = new InvoiceDto
        {
            CustomerId = estimate.CustomerId,
            CustomerName = estimate.CustomerName,
            JobId = estimate.JobId,
            JobName = estimate.JobName,
            EstimateId = estimateId,
            Subtotal = estimate.Subtotal,
            TaxRate = estimate.TaxRate,
            TaxAmount = estimate.TaxAmount,
            Total = estimate.Total,
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            Notes = $"Invoice from estimate {estimate.EstimateNumber}"
        };

        return await AddAsync(invoiceDto);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity is null) return;
        entity.IsDeleted = true;
        entity.DateUpdated = DateTime.UtcNow;
        await _repo.SaveAsync(entity);
    }
}
