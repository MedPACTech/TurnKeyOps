using System;
using MedInsights.Lib.Dtos;
using Microsoft.AspNetCore.Http;

namespace MedInsights.Services.Interfaces
{
    public interface IDocumentService
    {
        Task<DocumentDto> UploadFileAsync(DocumentUploadDto dto, CancellationToken ct);
        Task<DocumentDto> GetDocumentByIdAsync(Guid id, CancellationToken ct);
        Task<IEnumerable<DocumentDto>> GetDocumentsByUserAsync(Guid userId, CancellationToken ct);
        Task<IEnumerable<DocumentDto>> GetDocumentsByChatAsync(Guid chatId, CancellationToken ct);

        /// <summary>
        /// Get all documents by a list of Ids
        /// </summary>
        /// <param name="documentIds"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<IEnumerable<DocumentDto>> GetDocumentsByIdsAsync(IList<Guid> documentIds, CancellationToken ct);
        Task<IEnumerable<DocumentDto>> GetDocumentsByPatientAsync(Guid patientId, CancellationToken ct);
        Task<string> GetPromptAsync(IList<Guid> documentIds, CancellationToken ct);
    }
}
    