using MedInsights.Lib.Utils;
using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Enums;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;

namespace MedInsights.Authorization.Tests;

public sealed class JobReleaseServiceTests
{
    [Fact]
    public async Task ScheduledJobRequiresServiceApprovedInvoiceRelease()
    {
        var jobs = new Mock<IJobRepository>();
        var payloads = new Mock<IEstimateWorkflowPayloadStore>();
        var jobPayloads = new Mock<IJobWorkflowPayloadStore>();
        var invoices = new Mock<IInvoiceService>();
        var invoiceId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        jobs.Setup(item => item.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Job?)null);
        jobs.Setup(item => item.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Job>());
        var service = new JobService(jobs.Object, payloads.Object, jobPayloads.Object, invoices.Object, new User());
        var job = new JobDto { Id = Guid.NewGuid(), Name = "North lot", Status = JobStatus.Scheduled, InvoiceId = invoiceId };
        invoices.Setup(item => item.GetJobReleaseAsync(invoiceId, It.IsAny<CancellationToken>())).ReturnsAsync(new InvoiceJobReleaseDto
        {
            IsEligible = false,
            Reason = "Deposit is incomplete."
        });

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.AddAsync(job));

        Assert.Contains("Deposit is incomplete", error.Message);
        jobs.Verify(item => item.SaveAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class User : TurnKeyOps.Lib.Utils.IUserContext
    {
        public bool IsAuthenticated => true;
        public Guid TenantId => Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        public Guid UserId => Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        public AppTimeZone Timezone => AppTimeZone.Utc;
        public string FirstName => "Test";
        public string LastName => "User";
    }
}
