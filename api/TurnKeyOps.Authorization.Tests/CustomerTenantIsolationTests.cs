using MedInsights.Lib.Utils;
using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;

namespace MedInsights.Authorization.Tests;

public sealed class CustomerTenantIsolationTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task ExistingCustomerIdCannotBeReadUpdatedOrDeletedAcrossTenants()
    {
        var foreign = new Customer
        {
            Id = Guid.NewGuid(), PartitionKey = Guid.NewGuid().ToString(), RowKey = Guid.NewGuid().ToString(),
            FirstName = "Foreign", LastName = "Customer"
        };
        var repository = new Mock<ICustomerRepository>();
        repository.Setup(item => item.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(foreign);
        var service = new CustomerService(repository.Object, new User());

        Assert.Null(await service.GetAsync(foreign.Id));
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(new CustomerDto { Id = foreign.Id }));
        await service.DeleteAsync(foreign.Id);
        repository.Verify(item => item.SaveAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(item => item.GetAsync(
            TurnKeyOps.Lib.Utils.RepositoryKeyHelper.ToTenantPartitionKey(TenantId),
            TurnKeyOps.Lib.Utils.RepositoryKeyHelper.ToRowKey(foreign.Id),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    private sealed class User : TurnKeyOps.Lib.Utils.IUserContext
    {
        public bool IsAuthenticated => true;
        public Guid TenantId => CustomerTenantIsolationTests.TenantId;
        public Guid UserId => Guid.NewGuid();
        public AppTimeZone Timezone => AppTimeZone.Utc;
        public string FirstName => "Test";
        public string LastName => "User";
    }
}
