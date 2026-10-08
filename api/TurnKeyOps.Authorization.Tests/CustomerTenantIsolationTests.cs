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

    [Theory]
    [InlineData("residential")]
    [InlineData("commercial")]
    public async Task CustomerClassificationRoundTripsWithoutChangingIdentity(string type)
    {
        var repository=new Mock<ICustomerRepository>();
        var service=new CustomerService(repository.Object,new User());
        var id=Guid.NewGuid();
        var result=await service.AddAsync(new(){Id=id,CustomerType=type,FirstName="Alex",CompanyName="Acme"});
        Assert.Equal(id,result.Id);Assert.Equal(type,result.CustomerType);
        repository.Verify(r=>r.SaveAsync(It.Is<Customer>(c=>c.Id==id && c.CustomerType==type),It.IsAny<CancellationToken>()),Times.Once);
    }
    [Theory]
    [InlineData("unknown", "Alex", "Acme")]
    [InlineData("commercial", "Alex", "")]
    [InlineData("residential", "", "Acme")]
    public async Task InvalidClassificationOrMissingIdentityCannotBeSaved(string type,string name,string company)
    {
        var repository=new Mock<ICustomerRepository>();var service=new CustomerService(repository.Object,new User());
        await Assert.ThrowsAsync<ArgumentException>(()=>service.AddAsync(new(){CustomerType=type,FirstName=name,CompanyName=company}));
        repository.Verify(r=>r.SaveAsync(It.IsAny<Customer>(),It.IsAny<CancellationToken>()),Times.Never);
    }
    [Fact]
    public async Task OlderClientUpdatesPreserveExistingClassification()
    {
        var customer=new Customer{Id=Guid.NewGuid(),PartitionKey=TurnKeyOps.Lib.Utils.RepositoryKeyHelper.ToTenantPartitionKey(TenantId),CustomerType="commercial",CompanyName="Acme"};
        var repository=new Mock<ICustomerRepository>();repository.Setup(r=>r.GetAsync(customer.PartitionKey,It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        var result=await new CustomerService(repository.Object,new User()).UpdateAsync(new(){Id=customer.Id,CompanyName="Acme Updated"});
        Assert.Equal("commercial",result.CustomerType);Assert.Equal(customer.Id,result.Id);
        Assert.Null(TurnKeyOps.Services.Mappers.CustomerMapper.ToDto(new Customer{Id=Guid.NewGuid()}).CustomerType);
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
