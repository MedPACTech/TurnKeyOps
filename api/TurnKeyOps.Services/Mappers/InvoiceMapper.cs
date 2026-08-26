using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;

namespace TurnKeyOps.Services.Mappers;

public static class InvoiceMapper
{
    public static InvoiceDto ToDto(Invoice entity, IEnumerable<InvoiceLineItem>? lineItems = null)
    {
        var id = entity.Id != Guid.Empty ? entity.Id : Guid.Parse(entity.RowKey);
        return new InvoiceDto
        {
            Id = id,
            InvoiceNumber = entity.InvoiceNumber,
            Status = entity.Status,
            CustomerId = entity.CustomerId,
            CustomerName = entity.CustomerName,
            JobId = entity.JobId,
            JobName = entity.JobName,
            EstimateId = entity.EstimateId,
            QuoteRequestId = entity.QuoteRequestId,
            EstimateRevisionNumber = entity.EstimateRevisionNumber,
            Subtotal = entity.Subtotal,
            TaxRate = entity.TaxRate,
            TaxAmount = entity.TaxAmount,
            Total = entity.Total,
            AmountPaid = entity.AmountPaid,
            BalanceDue = entity.BalanceDue,
            IssueDate = DateTime.SpecifyKind(entity.IssueDate, DateTimeKind.Utc),
            DueDate = DateTime.SpecifyKind(entity.DueDate, DateTimeKind.Utc),
            PaidDate = entity.PaidDate,
            StripePaymentUrl = entity.StripePaymentUrl,
            Notes = entity.Notes,
            LineItems = lineItems?.OrderBy(item => item.SortOrder).Select(ToLineItemDto).ToList() ?? [],
            Version = entity.ETag.ToString(),
            DateCreated = DateTime.SpecifyKind(entity.DateCreated, DateTimeKind.Utc),
            DateUpdated = DateTime.SpecifyKind(entity.DateUpdated, DateTimeKind.Utc)
        };
    }

    public static Invoice ToEntity(InvoiceDto dto, string partitionKey)
    {
        return new Invoice
        {
            Id = dto.Id,
            PartitionKey = partitionKey,
            RowKey = RepositoryKeyHelper.ToRowKey(dto.Id),
            InvoiceNumber = dto.InvoiceNumber,
            Status = dto.Status,
            CustomerId = dto.CustomerId,
            CustomerName = dto.CustomerName,
            JobId = dto.JobId,
            JobName = dto.JobName,
            EstimateId = dto.EstimateId,
            QuoteRequestId = dto.QuoteRequestId,
            EstimateRevisionNumber = dto.EstimateRevisionNumber,
            Subtotal = dto.Subtotal,
            TaxRate = dto.TaxRate,
            TaxAmount = dto.TaxAmount,
            Total = dto.Total,
            AmountPaid = dto.AmountPaid,
            BalanceDue = dto.BalanceDue,
            IssueDate = DateTime.SpecifyKind(dto.IssueDate, DateTimeKind.Utc),
            DueDate = DateTime.SpecifyKind(dto.DueDate, DateTimeKind.Utc),
            PaidDate = dto.PaidDate,
            StripePaymentIntentId = dto.Payments.LastOrDefault(item =>
                string.Equals(item.Provider, "Stripe", StringComparison.OrdinalIgnoreCase))?.ExternalReference,
            StripePaymentUrl = dto.StripePaymentUrl,
            Notes = dto.Notes,
            DateCreated = dto.DateCreated ?? DateTime.UtcNow,
            DateUpdated = DateTime.UtcNow,
            IsDeleted = false
        };
    }

    public static InvoiceLineItemDto ToLineItemDto(InvoiceLineItem entity) => new()
    {
        Id = entity.Id,
        InvoiceId = entity.InvoiceId,
        SortOrder = entity.SortOrder,
        Description = entity.Description,
        Quantity = (decimal)entity.Quantity,
        Unit = entity.Unit,
        UnitPrice = entity.UnitPrice,
        LineTotal = entity.LineTotal
    };

    public static InvoiceLineItem ToLineItemEntity(InvoiceLineItemDto dto, string partitionKey, Guid invoiceId)
    {
        var id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id;
        return new InvoiceLineItem
        {
            Id = id,
            PartitionKey = partitionKey,
            RowKey = RepositoryKeyHelper.ToRowKey(id),
            InvoiceId = invoiceId,
            SortOrder = dto.SortOrder,
            Description = dto.Description,
            Quantity = (double)dto.Quantity,
            Unit = dto.Unit,
            UnitPrice = dto.UnitPrice,
            LineTotal = dto.LineTotal,
            DateCreated = DateTime.UtcNow,
            DateUpdated = DateTime.UtcNow
        };
    }
}
