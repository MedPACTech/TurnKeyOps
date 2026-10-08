using System.Security.Cryptography;
using MedInsights.AzureServices.Interfaces;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Services.Interfaces;
namespace TurnKeyOps.Services;
public sealed class SupplyFileService(ISupplyStore store,ISupplyAuthority authority,IUserContext user,IAzureBlobStorageService blobs)
{
    private static List<SupplyFile> Files(SupplyState s,string module,Guid id)=>module=="inventory"
        ?s.Receipts.SingleOrDefault(r=>r.Id==id)?.Files??throw new KeyNotFoundException("Receipt not found.")
        :s.Orders.SingleOrDefault(o=>o.Id==id)?.Files??throw new KeyNotFoundException("Order not found.");
    public async Task UploadAsync(string module,Guid id,string version,IReadOnlyCollection<QuoteRequestAttachmentUpload> uploads,CancellationToken ct)
    {
        await authority.RequireAsync(module,true,ct);var s=await store.ReadAsync(user.TenantId,ct);
        if(s.Version!=version)throw new InvalidOperationException("Supply changed. Refresh before uploading.");
        var files=Files(s,module,id);if(uploads.Count is 0 or >5||files.Count+uploads.Count>30)throw new ArgumentException("Choose one to five files, up to thirty per record.");
        foreach(var upload in uploads){
            if(upload.Length<=0||upload.Length>10*1024*1024||upload.ContentType is not ("image/jpeg" or "image/png" or "image/webp" or "application/pdf"))throw new ArgumentException("Use JPEG, PNG, WebP or PDF files up to 10 MB.");
            await using var bytes=new MemoryStream();await upload.Content.CopyToAsync(bytes,ct);if(bytes.Length>10*1024*1024)throw new ArgumentException("File too large.");bytes.Position=0;
            var file=new SupplyFile{Name=Path.GetFileName(upload.FileName),ContentType=upload.ContentType,Sha256=Convert.ToHexString(SHA256.HashData(bytes.ToArray()))};file.BlobName=$"{user.TenantId:N}/files/{id:N}/{file.Id:N}";
            await blobs.UploadAsync(SupplyStore.Container,file.BlobName,bytes,file.ContentType,new Dictionary<string,string>{{"tenantId",user.TenantId.ToString("N")}},ct);files.Add(file);
        }
        s.Audit.Add(new(Guid.NewGuid(),"files",user.UserId.ToString(),DateTime.UtcNow,$"{module}/{id}: {uploads.Count} files"));await store.SaveAsync(user.TenantId,s,version,ct);
    }
    public async Task<QuoteRequestAttachmentDownload> DownloadAsync(string module,Guid id,Guid fileId,CancellationToken ct)
    {
        await authority.RequireAsync(module,false,ct);var s=await store.ReadAsync(user.TenantId,ct);var file=Files(s,module,id).SingleOrDefault(f=>f.Id==fileId)??throw new KeyNotFoundException("File not found.");
        if(!file.BlobName.StartsWith($"{user.TenantId:N}/files/{id:N}/",StringComparison.Ordinal))throw new InvalidOperationException("Invalid file identity.");
        var stream=await blobs.OpenReadAsync(SupplyStore.Container,file.BlobName,ct);return new(file.Name,file.ContentType,stream.CanSeek?stream.Length:0,stream);
    }
}
