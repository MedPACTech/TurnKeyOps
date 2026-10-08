using TurnKeyOps.Lib.Dtos;
namespace TurnKeyOps.Services;
public static class FinanceIntegrity
{
    public static void ValidateTransition(FinanceState before,FinanceState after)
    {
        Preserve(before.Journals,after.Journals,j=>j.Id,"posted journal");
        Preserve(before.OpenItems,after.OpenItems,i=>i.Id,"open item");
        Preserve(before.Settlements,after.Settlements,i=>i.Id,"settlement");
        Preserve(before.BudgetReviews,after.BudgetReviews,i=>i.Id,"budget review");
        Preserve(before.JobChanges,after.JobChanges,i=>i.ChangeId,"accepted change value");
        Preserve(before.Costs,after.Costs,i=>i.Id,"job cost");
        Preserve(before.Reconciliations,after.Reconciliations,i=>i.Id,"reconciliation");
        Preserve(before.Audit,after.Audit,i=>i.CommandId,"audit evidence");
        foreach(var bill in before.Bills.Where(b=>b.JournalId is not null))
            if(!after.Bills.Any(b=>b.Id==bill.Id&&FinanceLedger.Hash(b)==FinanceLedger.Hash(bill)))throw new ArgumentException("Posted vendor bills are immutable.");
        foreach(var bank in before.BankTransactions)
            if(!after.BankTransactions.Any(b=>b.Id==bank.Id&&(bank.JournalId is null||b.JournalId==bank.JournalId&&b.ConfirmedBy==bank.ConfirmedBy)&&b.Amount==bank.Amount&&b.Date==bank.Date&&b.CashAccountId==bank.CashAccountId&&b.ExternalId==bank.ExternalId))throw new ArgumentException("Confirmed bank matches are immutable.");
        foreach(var account in before.Accounts.Where(a=>before.Journals.Any(j=>j.Lines.Any(l=>l.Account==a.Code))))
            if(!after.Accounts.Any(a=>a.Code==account.Code&&a.Type==account.Type&&a.NormalBalance==account.NormalBalance))throw new ArgumentException("Used account classifications are immutable.");
        if(after.Journals.Select(j=>(j.SourceType,j.SourceId)).Distinct().Count()!=after.Journals.Count)throw new ArgumentException("Duplicate journal source.");
        foreach(var j in after.Journals)
        {
            if(j.Lines.Any(l=>!after.Accounts.Any(a=>a.Code==l.Account)||l.Debit<0||l.Credit<0||(l.Debit==0)==(l.Credit==0)))throw new ArgumentException("Invalid immutable journal line.");
            if(j.Status!="POSTED"||j.Lines.Sum(l=>l.Debit)!=j.Lines.Sum(l=>l.Credit)||j.Lines.Sum(l=>l.Debit)<=0)throw new ArgumentException("Posted journal invariant failed.");
            if(j.Fingerprint!=FinanceLedger.Hash(new{date=j.Date,type=j.SourceType,source=j.SourceId,memo=j.Memo,lines=j.Lines,reverses=j.ReversesId}))throw new ArgumentException("Journal content fingerprint mismatch.");
        }
        if(after.Accounts.Count>0)
        {
            var asOf=DateOnly.MaxValue;
            if(FinanceLedger.AccountBalance(after,after.Policy.Accounts["ar"],asOf)!=after.OpenItems.Where(i=>i.Kind=="AR").Sum(i=>FinanceLedger.Balance(after,i,asOf)))throw new ArgumentException("AR subledger/control mismatch.");
            if(-FinanceLedger.AccountBalance(after,after.Policy.Accounts["ap"],asOf)!=after.OpenItems.Where(i=>i.Kind=="AP").Sum(i=>FinanceLedger.Balance(after,i,asOf)))throw new ArgumentException("AP subledger/control mismatch.");
        }
    }
    private static void Preserve<T,K>(IEnumerable<T> before,IEnumerable<T> after,Func<T,K> key,string label)where K:notnull
    {
        var current=after.ToDictionary(key);
        foreach(var old in before)if(!current.TryGetValue(key(old),out var value)||FinanceLedger.Hash(value)!=FinanceLedger.Hash(old))throw new ArgumentException($"Cannot edit or delete {label} history. Record a correction.");
    }
}
