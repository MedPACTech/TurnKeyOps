using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TurnKeyOps.Lib.Dtos;
namespace TurnKeyOps.Services;
public sealed class FinanceHttpBankFeedProvider(IHttpClientFactory clients,IConfiguration configuration)
{
    // Deployment-owned tenant credentials/allowlists are never accepted in commands or Finance payloads.
    public async Task<FinanceBankFeedBatch> ReadAsync(Guid tenant,FinanceBankConnection connection,CancellationToken ct=default)
    {
        var config=configuration.GetSection("Finance:BankFeeds:Tenants:"+tenant.ToString("D"));
        if(!connection.Enabled||config["Enabled"]!="true")throw new ArgumentException("Bank connection is disabled.");
        if(config["ProviderId"]!=connection.ProviderId||config["AccountReference"]!=connection.AccountReference)throw new ArgumentException("Bank account is not configured for this tenant/provider.");
        if(!Uri.TryCreate(config["BaseUrl"],UriKind.Absolute,out var baseUrl)||baseUrl.Scheme!="https"||!string.IsNullOrEmpty(baseUrl.UserInfo)||!string.IsNullOrEmpty(baseUrl.Query))throw new ArgumentException("Configure an HTTPS bank gateway URL.");
        var apiKey=config["ApiKey"];if(string.IsNullOrWhiteSpace(apiKey))throw new ArgumentException("Bank connection credentials are not configured.");
        var endpoint=new Uri(baseUrl.AbsoluteUri.TrimEnd('/')+"/transactions?account="+Uri.EscapeDataString(connection.AccountReference));
        using var request=new HttpRequestMessage(HttpMethod.Get,endpoint);request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",apiKey);
        using var response=await clients.CreateClient("FinanceBankFeed").SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
        if(!response.IsSuccessStatusCode)throw new InvalidOperationException("Bank gateway request failed; no ingestion was committed.");
        if(response.Content.Headers.ContentLength>2_000_000)throw new ArgumentException("Bank response exceeds supported size.");
        await using var stream=await response.Content.ReadAsStreamAsync(ct);using var bounded=new MemoryStream();var buffer=new byte[8192];int count;
        while((count=await stream.ReadAsync(buffer,ct))>0){if(bounded.Length+count>2_000_000)throw new ArgumentException("Bank response exceeds supported size.");await bounded.WriteAsync(buffer.AsMemory(0,count),ct);}
        FinanceBankTransaction[] transactions;
        try
        {
            using var payload=JsonDocument.Parse(bounded.ToArray());
            if(payload.RootElement.ValueKind!=JsonValueKind.Array)throw new JsonException();
            foreach(var row in payload.RootElement.EnumerateArray())
            {
                if(row.TryGetProperty("currency",out var currency)&&currency.GetString()!="USD")throw new ArgumentException("Bank gateway returned unsupported transaction currency.");
                if(row.TryGetProperty("accountReference",out var account)&&account.GetString()!=connection.AccountReference)throw new ArgumentException("Bank gateway returned a different source account.");
            }
            transactions=payload.RootElement.Deserialize<FinanceBankTransaction[]>(JobConfigurationService.Json)??throw new JsonException();
        }catch(JsonException){throw new ArgumentException("Bank gateway returned invalid normalized transactions.");}
        if(transactions.Length>10000||transactions.Any(t=>t is null))throw new ArgumentException("Bank gateway supports at most 10000 normalized transactions.");
        var normalized=transactions.Select(t=>new FinanceBankTransaction{CashAccountId=connection.CashAccountId,ExternalId=t.ExternalId,Date=t.Date,Amount=t.Amount,Reference=t.Reference}).ToArray();
        return new(connection.ProviderId+":"+tenant.ToString("N")+":"+connection.AccountReference,normalized);
    }
}
