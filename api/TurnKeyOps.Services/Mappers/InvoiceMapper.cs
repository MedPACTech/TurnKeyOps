using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;

namespace TurnKeyOps.Services.Mappers;

public static class InvoiceMapper
{
    public static InvoiceDto ToDto(Invoice entity)
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
            Subtotal = dto.Subtotal,
            TaxRate = dto.TaxRate,
            TaxAmount = dto.TaxAmount,
            Total = dto.Total,
            AmountPaid = dto.AmountPaid,
            BalanceDue = dto.BalanceDue,
            IssueDate = DateTime.SpecifyKind(dto.IssueDate, DateTimeKind.Utc),
            DueDate = DateTime.SpecifyKind(dto.DueDate, DateTimeKind.Utc),
            PaidDate = dto.PaidDate,
            Notes = dto.Notes,
            DateCreated = dto.DateCreated ?? DateTime.UtcNow,
            DateUpdated = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}
