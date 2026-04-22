using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Enums;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services.Interfaces;
using TurnKeyOps.Services.Mappers;

namespace TurnKeyOps.Services;

public class DashboardService : IDashboardService
{
    private readonly IJobRepository _jobRepo;
    private readonly IEstimateRepository _estimateRepo;
    private readonly IInvoiceRepository _invoiceRepo;
    private readonly ICalendarEventService _calendarService;
    private readonly IUserContext _userContext;

    public DashboardService(
        IJobRepository jobRepo,
        IEstimateRepository estimateRepo,
        IInvoiceRepository invoiceRepo,
        ICalendarEventService calendarService,
        IUserContext userContext)
    {
        _jobRepo = jobRepo;
        _estimateRepo = estimateRepo;
        _invoiceRepo = invoiceRepo;
        _calendarService = calendarService;
        _userContext = userContext;
    }

    private string PK() => RepositoryKeyHelper.ToTenantPartitionKey(_userContext.TenantId);

    public async Task<DashboardDto> GetDashboardAsync()
    {
        var pk = PK();
        var jobs = (await _jobRepo.GetAllAsync(false, false)).Where(x => x.PartitionKey == pk).ToList();
        var estimates = (await _estimateRepo.GetAllAsync(false, false)).Where(x => x.PartitionKey == pk).ToList();
        var invoices = (await _invoiceRepo.GetAllAsync(false, false)).Where(x => x.PartitionKey == pk).ToList();

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var weekEnd = now.AddDays(7);

        var upcoming = await _calendarService.GetByDateRangeAsync(now, weekEnd);

        return new DashboardDto
        {
            ActiveJobs = jobs.Count(j => !j.IsDeleted && j.Status is JobStatus.InProgress or JobStatus.Scheduled),
            PendingEstimates = estimates.Count(e => !e.IsDeleted && e.Status is EstimateStatus.Draft or EstimateStatus.Sent),
            OverdueInvoices = invoices.Count(i => !i.IsDeleted && i.Status == InvoiceStatus.Overdue),
            RevenueThisMonth = invoices
                .Where(i => !i.IsDeleted && i.PaidDate >= monthStart)
                .Sum(i => i.AmountPaid),
            OutstandingBalance = invoices
                .Where(i => !i.IsDeleted && i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.Void)
                .Sum(i => i.BalanceDue),
            UpcomingEvents = upcoming.Take(5).ToList()
        };
    }
}
