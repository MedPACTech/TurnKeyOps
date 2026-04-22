using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using MedInsights.AzureServices.Interfaces;
using MedInsights.Lib.Configurations;
using Microsoft.Extensions.Options;


namespace MedInsights.AzureServices
{
    public class AzureBlobStorageService : IAzureBlobStorageService
    {
        private readonly string _connectionString;
        private readonly BlobServiceClient _blobServiceClient;

        public AzureBlobStorageService(IOptions<AzureStorageSettings> appSettings)
        {
            var settings = appSettings.Value;
            _connectionString = settings.ConnectionString;

            // Azurite may lag latest service API versions; pin a compatible version for local emulator usage.
            if (_connectionString.Contains("UseDevelopmentStorage=true", StringComparison.OrdinalIgnoreCase))
            {
                var options = new BlobClientOptions(BlobClientOptions.ServiceVersion.V2021_12_02);
                _blobServiceClient = new BlobServiceClient(_connectionString, options);
            }
            else
            {
                _blobServiceClient = new BlobServiceClient(_connectionString);
            }
        }

        public async Task<bool> Save(string containerName, string blobName, byte[] data)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

                // Ensure the container exists (creates if it doesn’t)
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

                var blobClient = containerClient.GetBlobClient(blobName);


                using (Stream stream = new MemoryStream(data))
                {
                    await blobClient.UploadAsync(stream, new BlobUploadOptions
                    {
                        HttpHeaders = new BlobHttpHeaders { ContentType = "application/octet-stream" }
                    });
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading file: {ex.Message}");
                return false;
            }
        }

        public async Task SaveDocument(string containerName, string blobName, Stream content, string? contentType = null)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync();

            var blobClient = containerClient.GetBlobClient(blobName);

            var headers = new BlobHttpHeaders
            {
                ContentType = contentType ?? "application/octet-stream"
            };

            await blobClient.UploadAsync(content, new BlobUploadOptions
            {
                HttpHeaders = headers
            });
        }


        public async Task<byte[]> Get(string containerName, string blobName)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                var downloadResult = await blobClient.DownloadStreamingAsync();

                using (var memoryStream = new MemoryStream())
                {
                    await downloadResult.Value.Content.CopyToAsync(memoryStream);
                    return memoryStream.ToArray();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading file: {ex.Message}");
                return null;
            }
        }

        public string GetBlobUrl(string containerName, string blobName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            return blobClient.Uri.ToString();
        }

        public async Task<Stream> OpenReadAsync(string container, string blobName, CancellationToken ct = default)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(container);
            var blobClient = containerClient.GetBlobClient(blobName);
            var resp = await blobClient.DownloadStreamingAsync(cancellationToken: ct);
            // Caller disposes the stream
            return resp.Value.Content;
        }
    }
}
