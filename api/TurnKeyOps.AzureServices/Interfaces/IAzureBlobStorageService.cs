
namespace MedInsights.AzureServices.Interfaces
{
    public interface IAzureBlobStorageService
    {
        Task<bool> Save(string containerName, string blobName, byte[] data);
        Task SaveDocument(string containerName, string blobName, Stream content, string? contentType = null);
        Task<byte[]> Get(string containerName, string blobName);
        string GetBlobUrl(string containerName, string blobName);
        Task<Stream> OpenReadAsync(string container, string blobName, CancellationToken ct = default);
    }
}
