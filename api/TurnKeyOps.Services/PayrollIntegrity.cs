using TurnKeyOps.Lib.Dtos;
namespace TurnKeyOps.Services;
public static class PayrollIntegrity
{
    public static void ValidateTransition(PayrollState previous,PayrollState next)
    {
        PayrollEngine.ValidateConfiguration(next.Configuration);
        static bool Same<T>(T a,T b)=>PayrollEngine.Hash(a)==PayrollEngine.Hash(b);
        static void AppendOnly<T>(IEnumerable<T> old,IEnumerable<T> updated,Func<T,Guid> key){foreach(var item in old){var found=updated.SingleOrDefault(v=>key(v)==key(item));if(found==null||!Same(item,found))throw new ArgumentException("Immutable payroll history changed.");}}
        AppendOnly(previous.Audit,next.Audit,a=>a.CommandId);AppendOnly(previous.Events,next.Events,a=>a.Id);AppendOnly(previous.Profiles,next.Profiles,a=>a.Id);AppendOnly(previous.Actuals,next.Actuals,a=>a.Id);AppendOnly(previous.WageRequirements,next.WageRequirements,a=>a.Id);
        if(next.Time.Select(t=>t.Id).Distinct().Count()!=next.Time.Count||next.Batches.Select(b=>b.Id).Distinct().Count()!=next.Batches.Count)throw new ArgumentException("Duplicate payroll identity.");
        foreach(var old in previous.Time){var current=next.Time.SingleOrDefault(t=>t.Id==old.Id)??throw new ArgumentException("Original time cannot be removed.");if(old.Status is "APPROVED" or "EXPORTED" or "CORRECTED"){
            if(!Same(old with{Status=current.Status},current)||old.Status=="EXPORTED"&&current.Status!="EXPORTED"||old.Status=="CORRECTED"&&current.Status!="CORRECTED"||old.Status=="APPROVED"&&current.Status is not("APPROVED" or "EXPORTED" or "CORRECTED"))throw new ArgumentException("Approved time is immutable except explicit correction/status transitions.");
            if(current.Status=="CORRECTED"&&!next.Time.Any(t=>t.CorrectsId==old.Id))throw new ArgumentException("Correction lineage required.");}}
        foreach(var b in previous.Batches){var n=next.Batches.SingleOrDefault(v=>v.Id==b.Id)??throw new ArgumentException("Batch cannot be removed.");if(!Same(b with{Status=n.Status,Artifact=n.Artifact,ArtifactHash=n.ArtifactHash},n)||b.Status=="PREPARED"&&n.Status is not("PREPARED" or "EXPORTED" or "VOID")||b.Status=="EXPORTED"&&n.Status is not("EXPORTED" or "PROCESSED")||b.Status=="PROCESSED"&&n.Status!="PROCESSED"||b.Status=="VOID"&&n.Status!="VOID")throw new ArgumentException("Batch snapshot is immutable.");if(b.Artifact!=null&&(b.Artifact!=n.Artifact||b.ArtifactHash!=n.ArtifactHash))throw new ArgumentException("Export artifact is immutable.");}
        foreach(var e in next.Events)if(e.Hash!=PayrollEngine.Hash(e.Payload))throw new ArgumentException("Payroll event integrity failure.");
        foreach(var b in next.Batches.Where(b=>b.Artifact!=null))if(b.ArtifactHash!=PayrollEngine.Hash(b.Artifact))throw new ArgumentException("Export integrity failure.");
    }
}
