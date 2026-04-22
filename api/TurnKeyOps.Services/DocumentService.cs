using System.Text.Json.Nodes;
using MedInsights.AzureServices.Interfaces;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Models;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.BackgroundServices.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace MedInsights.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly IAzureBlobStorageService _azureBlobStorageService;
        private readonly IUserContext _userContext;
        private readonly IFileTextExtractorService _textExtractor;

        public DocumentService(
            IDocumentRepository documentRepository,
            IAzureBlobStorageService azureBlobStorageService,
            IFileTextExtractorService textExtractor,
            IUserContext userContext)
        {
            _documentRepository = documentRepository;
            _azureBlobStorageService = azureBlobStorageService;
            _textExtractor = textExtractor;
            _userContext = userContext;
        }

        private string PartitionKeyForCurrent()
        {
            return EntityKeyPolicy.TenantPartition(_userContext.TenantId);
        }

        //TODO: Another base service later option?
        private string RowKeyForCurrent(Guid id)
        {
            return EntityKeyPolicy.Row(id);
        }


        public async Task<DocumentDto> UploadFileAsync(DocumentUploadDto dto, CancellationToken ct)
        {
            if (dto?.File == null || dto.File.Length == 0)
                throw new ArgumentException("File is required.", nameof(dto.File));
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            var fileName = dto.File.FileName;

            // ✅ Upload to blob storage
            await using (var stream = dto.File.OpenReadStream())
            {
                await _azureBlobStorageService.SaveDocument("documents", fileName, stream, dto.File.ContentType);
            }

            // ✅ Blob URL for reference
            var blobUrl = _azureBlobStorageService.GetBlobUrl("documents", fileName);

            // ✅ Create & persist metadata
            var documentEntity = new Document
            {
                Id = Guid.NewGuid(),
                PartitionKey = PartitionKeyForCurrent(),
                RowKey = string.Empty,
                UserId = _userContext.UserId,
                FileName = fileName,
                BlobUrl = blobUrl,
                Size = dto.File.Length,
                ContentType = dto.File.ContentType ?? "application/octet-stream",
                Category = dto.Category ?? string.Empty,
                UploadedAt = DateTime.UtcNow,
                // Optional status fields if you have them:
                TextContent = "",
                PatientId = dto.PatientId,
                ChatId = dto.ChatId,
                IsDeleted = false
            };
            documentEntity.RowKey = RowKeyForCurrent(documentEntity.Id);

            documentEntity = await _documentRepository.SaveAsync(documentEntity, ct);

            // ✅ Kick off extraction (non-blocking)
            _ = Task.Run(async () =>
            {
                // Use a fresh token or CancellationToken.None so it survives the HTTP request scope
                await ExtractAndPersistTextAsync(documentEntity, CancellationToken.None);
            });

            // ✅ Return result
            var documentResult = DocumentMapper.MapToResultDto(documentEntity);
            documentResult.BlobUrl = blobUrl;
            documentResult.Message = "File uploaded successfully. Text extraction started.";
            return documentResult;
        }

        public async Task<DocumentDto> GetDocumentByIdAsync(Guid id, CancellationToken ct)
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            var partitionKey = PartitionKeyForCurrent();
            var rowKey = RowKeyForCurrent(id);

            var documentEntity = await _documentRepository.GetAsync(partitionKey, rowKey, ct);
            if (documentEntity == null)
                throw new KeyNotFoundException("Document not found.");

            var documentResult = DocumentMapper.MapToResultDto(documentEntity);
            return documentResult;
        }

        //get but patient
        public async Task<IEnumerable<DocumentDto>> GetDocumentsByPatientAsync(Guid patientId, CancellationToken ct)
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            var partitionKey = PartitionKeyForCurrent();


            var documentEntities = await _documentRepository.GetDocumentsByPatientIdAsync(patientId, ct);
            if (documentEntities == null)
                throw new KeyNotFoundException("Document not found.");

            var documentResult = documentEntities.Select(DocumentMapper.MapToResultDto).ToList();
            return documentResult;
        }

        /// <summary>
        /// Get all documents by a list of Ids
        /// </summary>
        /// <param name="documentIds"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="UnauthorizedAccessException"></exception>
        /// <exception cref="KeyNotFoundException"></exception>
        public async Task<IEnumerable<DocumentDto>> GetDocumentsByIdsAsync(IList<Guid> documentIds, CancellationToken ct)
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            var partitionKey = PartitionKeyForCurrent();

            var documentEntities = await _documentRepository.GetDocumentsByIdsAsync(documentIds, ct);
            if (documentEntities == null)
                throw new KeyNotFoundException("Document not found.");

            var documentResult = documentEntities.Select(DocumentMapper.MapToResultDto).ToList();
            return documentResult;
        }

        public async Task<IEnumerable<DocumentDto>> GetDocumentsByUserAsync(Guid userId, CancellationToken ct)
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            var partitionKey = PartitionKeyForCurrent();

            var documentEntities = await _documentRepository.GetDocumentsByUserIdAsync(userId, ct);

            var documentResults = documentEntities
                .Select(DocumentMapper.MapToResultDto)
                .ToList();

            return documentResults;
        }

        public async Task<IEnumerable<DocumentDto>> GetDocumentsByChatAsync(Guid chatId, CancellationToken ct)
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            var partitionKey = PartitionKeyForCurrent();

            var documentEntities = await _documentRepository.GetDocumentsByChatIdAsync(chatId, ct);

            var documentResults = documentEntities
                .Select(DocumentMapper.MapToResultDto)
                .ToList();

            return documentResults;
        }

        public async Task<string> GetPromptAsync(IList<Guid> documentIds, CancellationToken ct)
        {
            var documents = new List<Document>();
            foreach (var docId in documentIds)
            {
                var partitionKey = PartitionKeyForCurrent();
                var rowKey = RowKeyForCurrent(docId);
                var documentEntity = await _documentRepository.GetAsync(partitionKey, rowKey, CancellationToken.None);
                if (documentEntity != null)
                {
                    documents.Add(documentEntity);
                }
            }

           var prompt = DocumentUtilities.BuildDocumentsContextPrompt(documents);
           return prompt;
        } 

        private async Task ExtractAndPersistTextAsync(Document documentEntity, CancellationToken ct)
        {
            try
            {
                // 1) Re-open the blob (no need to keep the original request stream)
                await using var blobStream = await _azureBlobStorageService
                    .OpenReadAsync("documents", documentEntity.FileName, ct);

                // 2) Extract text
                var extraction = await _textExtractor.ExtractAsync(
                    content: blobStream,
                    fileName: documentEntity.FileName,
                    contentType: documentEntity.ContentType,
                    options: null,
                    ct: ct
                );

                // 3) Cleanup Text 
                var cleaned = DocumentUtilities.NormalizeDocument(extraction.Text);

                // 3) Persist text & a few useful facts
                documentEntity.TextContent = cleaned;
                // max text size for the field is 16,000 characters in Azure Table Storage
                documentEntity.DetectedContainerType = extraction.ContainerType.ToString(); // optional column
                documentEntity.ContentNature = extraction.Nature;                           // optional column
                //uploadEntity.LastProcessedUtc = DateTime.UtcNow;                          // optional column
                documentEntity.ETag = Azure.ETag.All; // match any

                await _documentRepository.SaveAsync(documentEntity, ct);
            }
            catch (OperationCanceledException)
            {
                // optional: mark status
                //_logger.LogWarning("Text extraction canceled for {File}", uploadEntity.FileName);
            }
            catch (Exception ex)
            {
                // optional: store an error field/flag
                //_logger.LogError(ex, "Failed to extract text for {File}", uploadEntity.FileName);
                documentEntity.TextExtractionError = ex.Message; // optional column
                //uploadEntity.LastProcessedUtc = DateTime.UtcNow;
                try { await _documentRepository.SaveAsync(documentEntity, ct); } catch { /* best effort */ }
            }
        }
    }
}

