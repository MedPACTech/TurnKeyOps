using System.Text.Json;
using MedInsights.Lib;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Moq;
using TurnKeyOps.API.Controllers;
using TurnKeyOps.Lib.Configurations;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services.Interfaces;

namespace MedInsights.Authorization.Tests;
public sealed class LocksmithFieldInvoiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid RequestId = Guid.NewGuid();
    private static readonly Guid InvoiceId = Guid.NewGuid();
    private static string Partition => TurnKeyOps.Lib.Utils.RepositoryKeyHelper.ToTenantPartitionKey(TenantId);

    [Fact]
    public async Task FieldListOnlyReturnsEligibleTenantSourceInvoices()
    {
        var fixture = Create();
        fixture.Invoices.Setup(value => value.GetPagedAsync(100, null)).ReturnsAsync((new[] { fixture.Invoice, new InvoiceDto { Id = Guid.NewGuid() } }.AsEnumerable(), (string?)null));
        var result = Assert.IsType<OkObjectResult>(await fixture.Controller.List(default));
        var data = JsonSerializer.SerializeToElement(result.Value).GetProperty("Data");
        Assert.Equal(1, data.GetArrayLength());
        Assert.Equal(InvoiceId, data[0].GetProperty("Id").GetGuid());
    }

    [Theory]
    [InlineData("commercial", false, false)]
    [InlineData("residential", true, false)]
    [InlineData("residential", false, true)]
    public async Task FieldDeniesOtherJobTypesOtherAssigneesAndOtherTenants(string jobType, bool otherAssignee, bool otherTenant)
    {
        var fixture = Create(jobType: jobType, otherAssignee: otherAssignee, otherTenant: otherTenant);
        Assert.IsType<NotFoundResult>(await fixture.Controller.Get(InvoiceId, default));
        Assert.IsType<NotFoundResult>(await fixture.Controller.Complete(InvoiceId, new(), default));
        fixture.Invoices.Verify(value => value.RecordCompletionSignatureAsync(It.IsAny<Guid>(), It.IsAny<InvoiceCompletionSignatureInputDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("Removed")]
    [InlineData("Invited")]
    public async Task FieldDeniesInactiveMembership(string membershipStatus)
    {
        var fixture = Create(status: membershipStatus);
        Assert.IsType<ForbidResult>(await fixture.Controller.List(default));
        fixture.Invoices.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MissingExplicitJobLinkFailsClosedAndOwnAssignedJobCanSign()
    {
        var fixture = Create();
        fixture.Invoice.JobId = Guid.NewGuid();
        Assert.IsType<NotFoundResult>(await fixture.Controller.Get(InvoiceId, default));
        fixture.Invoice.JobId = null;
        fixture.Invoices.Setup(value => value.RecordCompletionSignatureAsync(InvoiceId, It.IsAny<InvoiceCompletionSignatureInputDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(fixture.Invoice);
        Assert.IsType<OkObjectResult>(await fixture.Controller.Complete(InvoiceId, new(), default));
        fixture.Invoices.Verify(value => value.RecordCompletionSignatureAsync(InvoiceId, It.IsAny<InvoiceCompletionSignatureInputDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvoiceWithoutLinkedJobFailsClosedAndMixedAssignmentsAreDenied()
    {
        var fixture = Create();
        fixture.Jobs.Setup(value => value.ListAsync(Partition, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Job>());
        Assert.IsType<NotFoundResult>(await fixture.Controller.Get(InvoiceId, default));
        fixture.Jobs.Setup(value => value.ListAsync(Partition, It.IsAny<CancellationToken>())).ReturnsAsync(new[] {
            new Job { Id = Guid.NewGuid(), PartitionKey = Partition, QuoteRequestId = RequestId, AssignedTechnicianMembershipId = MemberId },
            new Job { Id = Guid.NewGuid(), PartitionKey = Partition, QuoteRequestId = RequestId, AssignedTechnicianMembershipId = Guid.NewGuid() }
        });
        Assert.IsType<NotFoundResult>(await fixture.Controller.Complete(InvoiceId, new(), default));
        fixture.Invoices.Verify(value => value.RecordCompletionSignatureAsync(It.IsAny<Guid>(), It.IsAny<InvoiceCompletionSignatureInputDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("staff", true, false)]
    [InlineData("member", true, false)]
    [InlineData("admin", true, true)]
    [InlineData("owner", true, true)]
    [InlineData("staff", false, true)]
    public async Task GenericInvoiceGuardBlocksLocksmithFieldBypassButPreservesAdminsAndOtherTrades(string role, bool locksmith, bool allowed)
    {
        var fixture = Create(role: role, locksmith: locksmith);
        var options = Options.Create(new QuoteRequestTenantOptions { Tenants = locksmith ? new() { ["carlzipf"] = new() { TenantId = TenantId } } : new() });
        var filter = new LocksmithInvoiceAdminFilter(fixture.User.Object, fixture.Members.Object, fixture.Settings.Object, options);
        var action = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        var context = new ActionExecutingContext(action, [], new Dictionary<string, object?>(), new object());
        var executed = false;
        await filter.OnActionExecutionAsync(context, () => { executed = true; return Task.FromResult(new ActionExecutedContext(action, [], new object())); });
        Assert.Equal(allowed, executed);
        if (!allowed) Assert.IsType<ForbidResult>(context.Result);
    }

    [Fact]
    public async Task GenericCarlZipfGuardStillDeniesWhenSettingsAreAbsent()
    {
        var fixture = Create(locksmith: false);
        var filter = new LocksmithInvoiceAdminFilter(fixture.User.Object, fixture.Members.Object, fixture.Settings.Object,
            Options.Create(new QuoteRequestTenantOptions { Tenants = new() { ["carlzipf"] = new() { TenantId = TenantId } } }));
        var action = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        var context = new ActionExecutingContext(action, [], new Dictionary<string, object?>(), new object());
        await filter.OnActionExecutionAsync(context, () => throw new InvalidOperationException("Field user must not reach generic invoice operation"));
        Assert.IsType<ForbidResult>(context.Result);
    }

    private static Fixture Create(string jobType = "residential", bool otherAssignee = false, bool otherTenant = false, string status = "Active", string role = "staff", bool locksmith = true)
    {
        var user = new Mock<IUserContext>(); user.SetupGet(value => value.IsAuthenticated).Returns(true); user.SetupGet(value => value.TenantId).Returns(TenantId); user.SetupGet(value => value.UserId).Returns(UserId);
        var members = new Mock<ITenantMembershipRepository>();
        members.Setup(value => value.GetByUserIdAsync(EntityKeyPolicy.TenantPartition(TenantId), UserId, It.IsAny<CancellationToken>())).ReturnsAsync(new TenantMembership { Id = MemberId, TenantId = TenantId, UserId = UserId, MembershipStatus = status, Role = role });
        var settings = new Mock<ITenantSettingsRepository>();
        settings.Setup(value => value.GetAsync(Partition, "SETTINGS|OPERATIONAL", It.IsAny<CancellationToken>(), false)).ReturnsAsync(locksmith ? new TenantSettingsDocument { ValuesJson = JsonSerializer.Serialize(new { locksmith = new { techCapabilities = new Dictionary<string, string[]> { [MemberId.ToString()] = ["residential"] } } }) } : null);
        var requests = new Mock<IQuoteRequestRepository>();
        requests.Setup(value => value.GetAsync(Partition, TurnKeyOps.Lib.Utils.RepositoryKeyHelper.ToRowKey(RequestId), It.IsAny<CancellationToken>())).ReturnsAsync(new QuoteRequest { Id = RequestId, TenantId = otherTenant ? Guid.NewGuid() : TenantId, PropertyType = jobType });
        var jobs = new Mock<IJobRepository>();
        jobs.Setup(value => value.ListAsync(Partition, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { new Job { Id = Guid.NewGuid(), PartitionKey = Partition, QuoteRequestId = RequestId, LocksmithJobType = jobType, AssignedTechnicianMembershipId = otherAssignee ? Guid.NewGuid() : MemberId } });
        var invoice = new InvoiceDto { Id = InvoiceId, QuoteRequestId = RequestId };
        var invoices = new Mock<IInvoiceService>(); invoices.Setup(value => value.GetAsync(InvoiceId)).ReturnsAsync(invoice);
        var controller = new LocksmithFieldInvoicesController(invoices.Object, requests.Object, jobs.Object, members.Object, settings.Object, user.Object) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        return new(controller, invoice, invoices, members, settings, user, jobs);
    }
    private sealed record Fixture(LocksmithFieldInvoicesController Controller, InvoiceDto Invoice, Mock<IInvoiceService> Invoices, Mock<ITenantMembershipRepository> Members, Mock<ITenantSettingsRepository> Settings, Mock<IUserContext> User, Mock<IJobRepository> Jobs);
}
