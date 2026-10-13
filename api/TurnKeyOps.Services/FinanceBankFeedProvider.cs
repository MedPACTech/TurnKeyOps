using TurnKeyOps.Lib.Dtos;
namespace TurnKeyOps.Services;

public sealed record FinanceBankFeedBatch(string ProviderAccountKey, FinanceBankTransaction[] Transactions);
// Providers retrieve/parse data; all writes, review and idempotency remain inside Finance operations.
public interface IBankFeedProvider
{
    string ProviderId { get; }
    Task<FinanceBankFeedBatch> ReadAsync(string AccountReference, CancellationToken ct=default);
}

public static class FinanceBankFeedCsv
{
    // Explicit normalized format: externalId,date,amount,reference. Cash account is selected by the owner.
    public static FinanceBankFeedBatch Parse(string csv,string providerAccountKey,string cashAccountId)
    {
        FinanceLedger.Required(providerAccountKey);FinanceLedger.Required(cashAccountId);
        if(csv.Length>2_000_000)throw new ArgumentException("Bank CSV exceeds supported size.");
        using var parser=new Microsoft.VisualBasic.FileIO.TextFieldParser(new StringReader(csv)){TextFieldType=Microsoft.VisualBasic.FileIO.FieldType.Delimited,HasFieldsEnclosedInQuotes=true};
        parser.SetDelimiters(",");string[]? header;try{header=parser.ReadFields();}catch(Microsoft.VisualBasic.FileIO.MalformedLineException){throw new ArgumentException("Malformed bank CSV header.");}
        if(header is null||!header.SequenceEqual(new[]{"externalId","date","amount","reference"}))throw new ArgumentException("Use normalized bank CSV headers externalId,date,amount,reference.");
        var rows=new List<FinanceBankTransaction>();
        while(!parser.EndOfData)
        {
            string[]? values;try{values=parser.ReadFields();}catch(Microsoft.VisualBasic.FileIO.MalformedLineException){throw new ArgumentException("Malformed bank CSV row.");}
            if(values is null||values.Length!=4||!DateOnly.TryParseExact(values[1],"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var date)||!decimal.TryParse(values[2],System.Globalization.NumberStyles.AllowLeadingSign|System.Globalization.NumberStyles.AllowDecimalPoint,System.Globalization.CultureInfo.InvariantCulture,out var amount))throw new ArgumentException("Bank CSV requires four fields, ISO date and plain decimal amount.");
            FinanceLedger.Required(values[0]);FinanceLedger.Money(amount);
            rows.Add(new(){CashAccountId=cashAccountId,ExternalId=values[0],Date=date,Amount=amount,Reference=values[3]});if(rows.Count>10000)throw new ArgumentException("Bank feed batch exceeds supported size.");
        }
        if(rows.Count==0)throw new ArgumentException("Bank feed requires at least one transaction.");
        return new(providerAccountKey,rows.ToArray());
    }
}
