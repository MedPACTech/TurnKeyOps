using Azure.Data.Tables;
using MedInsights.Lib.Configurations;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public class SystemErrorRepository : ISystemErrorRepository
    {
        private readonly TableClient _tableClient;

        public SystemErrorRepository(IOptions<AzureStorageSettings> azureStorageSettings)
        {
            var connectionString = azureStorageSettings.Value.ConnectionString;
            var serviceClient = new TableServiceClient(connectionString);
            _tableClient = serviceClient.GetTableClient("SystemErrors");
            _tableClient.CreateIfNotExists();
        }

        public async Task SaveAsync(SystemError error)
        {
            await _tableClient.AddEntityAsync(error);
        }
    }

}
