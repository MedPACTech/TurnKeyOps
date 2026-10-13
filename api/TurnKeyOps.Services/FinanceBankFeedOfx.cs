using System.Globalization;
using System.Text.RegularExpressions;
using TurnKeyOps.Lib.Dtos;
namespace TurnKeyOps.Services;
public static class FinanceBankFeedOfx
{
    // OFX 1.x SGML and OFX/QFX XML share these financial statement tags; no XML entities execute.
    public static FinanceBankFeedBatch Parse(string document,string providerAccountKey,string cashAccountId)
    {
        FinanceLedger.Required(providerAccountKey);FinanceLedger.Required(cashAccountId);
        if(document.Length>2_000_000||!document.Contains("<OFX",StringComparison.OrdinalIgnoreCase)||document.Contains("<!DOCTYPE",StringComparison.OrdinalIgnoreCase)||document.Contains("<!ENTITY",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Use a bounded OFX/QFX bank statement without external entities.");
        string Tag(string body,string tag)=>System.Net.WebUtility.HtmlDecode(Regex.Match(body,"<"+tag+">\\s*([^<\\r\\n]+)",RegexOptions.IgnoreCase,TimeSpan.FromSeconds(1)).Groups[1].Value.Trim());
        var currencies=Regex.Matches(document,"<CURDEF>\\s*([^<\\r\\n]+)",RegexOptions.IgnoreCase,TimeSpan.FromSeconds(1)).Select(m=>m.Groups[1].Value.Trim()).Distinct().ToArray();
        if(currencies.Length!=1||currencies[0]!="USD")throw new ArgumentException("OFX/QFX requires an explicit USD statement currency.");
        var accounts=Regex.Matches(document,"<ACCTID>\\s*([^<\\r\\n]+)",RegexOptions.IgnoreCase,TimeSpan.FromSeconds(1)).Select(m=>m.Groups[1].Value.Trim()).Distinct().ToArray();
        if(accounts.Length!=1)throw new ArgumentException("Import one explicit bank account statement at a time.");
        var rows=new List<FinanceBankTransaction>();
        foreach(Match match in Regex.Matches(document,"<STMTTRN>(.*?)</STMTTRN>",RegexOptions.IgnoreCase|RegexOptions.Singleline,TimeSpan.FromSeconds(1)))
        {
            var id=Tag(match.Groups[1].Value,"FITID");var date=Tag(match.Groups[1].Value,"DTPOSTED");var amount=Tag(match.Groups[1].Value,"TRNAMT");FinanceLedger.Required(id);
            if(date.Length<8||!DateOnly.TryParseExact(date[..8],"yyyyMMdd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var day)||!decimal.TryParse(amount,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var value))throw new ArgumentException("OFX/QFX transaction date or amount is invalid.");FinanceLedger.Money(value);
            rows.Add(new(){ExternalId=id,CashAccountId=cashAccountId,Date=day,Amount=value,Reference=(Tag(match.Groups[1].Value,"NAME")+" "+Tag(match.Groups[1].Value,"MEMO")).Trim()});if(rows.Count>10000)throw new ArgumentException("OFX/QFX batch exceeds supported size.");
        }
        if(rows.Count==0)throw new ArgumentException("OFX/QFX statement has no complete transactions.");
        return new(providerAccountKey,rows.ToArray());
    }
}
