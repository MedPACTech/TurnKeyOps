using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TurnKeyOps.Lib.Dtos;
namespace TurnKeyOps.Services;
public sealed class FinanceAccrualReversalRunner(IFinanceStore store)
{
    public async Task<int> RunTenantAsync(Guid tenant,DateOnly asOf,CancellationToken ct=default)
    {
        if(tenant==Guid.Empty||asOf==default)throw new ArgumentException("Tenant and as-of date required.");
        var initial=await store.ReadAsync(tenant,ct);if(!initial.Operations.ReversalPolicy.Enabled)return 0;var processed=0;
        foreach(var id in initial.Operations.Accruals.Where(a=>a.ReversalJournalId is null&&a.ReversalDate<=asOf).Select(a=>a.Id))
        {
            var state=await store.ReadAsync(tenant,ct);if(!state.Operations.ReversalPolicy.Enabled)return processed;
            var accrual=state.Operations.Accruals.Single(a=>a.Id==id);if(accrual.ReversalJournalId is not null)continue;
            var attemptId=new Guid(Convert.FromHexString(FinanceLedger.Hash(new{tenant,id,asOf,Action="scheduled-accrual-reversal"}))[..16]);
            if(state.Operations.ReversalAttempts.Any(a=>a.Id==attemptId))continue;
            var expected=state.Version;var candidate=JobConfigurationService.Clone(state);var status="POSTED";var reason="Tenant owner policy authorized due accrual reversal.";
            try{FinanceOperationsEngine.Apply(candidate,new(){Action="reverse-accrual",TargetId=id},"system:accrual-reversal");}
            catch(ArgumentException ex){candidate=state;status="BLOCKED";reason=ex.Message;}
            candidate.Operations.ReversalAttempts.Add(new(attemptId,id,asOf,status,reason));
            candidate.Audit.Add(new(attemptId,"scheduled-accrual-reversal",id.ToString(),"system:accrual-reversal",DateTime.UtcNow,reason,FinanceLedger.Hash(new{id,asOf,status,Policy=state.Operations.ReversalPolicy})));
            await store.SaveAsync(tenant,candidate,expected,ct);if(status=="POSTED")processed++;
        }
        return processed;
    }
}
public sealed class FinanceAccrualReversalWorker(IServiceScopeFactory scopes,IConfiguration config,ILogger<FinanceAccrualReversalWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if(config["Finance:AccrualReversals:Enabled"]!="true")return;
        using var timer=new PeriodicTimer(TimeSpan.FromMinutes(15));
        do
        {
            try
            {
                using var scope=scopes.CreateScope();var tenants=await scope.ServiceProvider.GetRequiredService<ITenantMembershipRepository>().GetTenantIdsAsync(ct);
                foreach(var tenant in tenants)
                {try{await scope.ServiceProvider.GetRequiredService<FinanceAccrualReversalRunner>().RunTenantAsync(tenant,DateOnly.FromDateTime(DateTime.UtcNow),ct);}catch(OperationCanceledException)when(ct.IsCancellationRequested){return;}catch(Exception ex){logger.LogError(ex,"Accrual reversal tenant {TenantId} remains pending; next scan retries.",tenant);}}
            }
            catch(OperationCanceledException)when(ct.IsCancellationRequested){return;}
            catch(Exception ex){logger.LogError(ex,"Accrual reversal scan failed; next scan retries.");}
        }while(await timer.WaitForNextTickAsync(ct));
    }
}
