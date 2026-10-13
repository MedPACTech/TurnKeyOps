import {fail,type RequestEvent} from '@sveltejs/kit';
import {leadApi} from './leads';
import {csv,type FinanceWorkspace,type FinanceAnalysis,type FinanceMigrationPreview} from '$lib/finance';
export const load=async(event:RequestEvent)=>{
 event.setHeaders({'Cache-Control':'private, no-store'});
 const query=new URLSearchParams();for(const key of ['from','asOf'])if(event.url.searchParams.get(key))query.set(key,event.url.searchParams.get(key)!);
 return {workspace:await leadApi<FinanceWorkspace>(event,`finance?${query}`),basePath:event.url.pathname.split('/finance')[0]};
};
export const actions={save:async(event:RequestEvent)=>{
 const f=await event.request.formData();const str=(k:string)=>String(f.get(k)??'');const num=(k:string)=>Number(str(k)||0);
 const action=str('action');const c:Record<string,unknown>={id:str('commandId')||crypto.randomUUID(),expectedVersion:str('version'),action,target:str('target'),reason:str('reason'),date:str('date')||'0001-01-01',endDate:str('endDate')||'0001-01-01',amount:num('amount'),openingBalance:num('openingBalance'),reference:str('reference'),method:str('method')||'manual',partyId:str('partyId')||'00000000-0000-0000-0000-000000000000',jobId:str('jobId')||null,category:str('category'),costCode:str('costCode')||null,account:str('account'),sourceKey:str('sourceKey'),hours:str('hours')?num('hours'):null,rate:str('rate')?num('rate'):null,complete:f.get('complete')==='on'};
 try{
  if(action==='bob'){
   const tool=str('tool')||'finance.close-readiness';if(!['finance.ar','finance.ap','finance.job-health','finance.cash','finance.close-readiness'].includes(tool))throw new Error('Choose a supported Finance review.');
   const r=await leadApi<{resultJson:string}>(event,'finance/bob',{toolKey:tool,idempotencyKey:crypto.randomUUID(),input:{asOf:str('asOf')}});
   const raw=JSON.parse(r.resultJson||'null');
   // Bob storage minimizes with PascalCase; never render a truncated string as if it were a complete analysis.
   const normalized=raw&&Object.fromEntries(Object.entries(raw).map(([k,v])=>[k[0].toLowerCase()+k.slice(1),v]));
   if(!normalized?.title||normalized.truncated)return{message:'This review exceeded Bob’s stored response limit. Review the selected Finance view for the complete source records.',failed:false};
   const analysis={...normalized,findings:(normalized.findings as Record<string,unknown>[]??[]).map(f=>Object.fromEntries(Object.entries(f).map(([k,v])=>[k[0].toLowerCase()+k.slice(1),v])))} as FinanceAnalysis;
   return{message:'Financial review recorded. No messages, payments or postings were made.',analysis,failed:false};
  }
  if(action==='reverse-opening-credit'){
   await leadApi(event,'finance/migration/reverse-credit',{id:c.id,expectedVersion:c.expectedVersion,date:str('date'),applicationId:str('target'),reason:str('reason')});
   return{message:'Opening credit unapplied through an audited compensating entry. Original records are preserved.',failed:false};
  }
  if(action==='opening-credit'){
   await leadApi(event,'finance/migration/apply-credit',{id:c.id,expectedVersion:c.expectedVersion,date:str('date'),creditItemId:str('creditItemId'),targetItemId:str('targetItemId'),amount:num('amount'),reason:str('reason')});
   return{message:'Opening credit applied to the reviewed same-party document. No revenue, expense, tax or cash was invented.',failed:false};
  }
  if(action==='migration-template'){
   const template=await leadApi<{request:Record<string,unknown>}>(event,'finance/migration/template');
   return{message:'Migration template loaded with current Finance and purchasing versions. Map and reconcile source exports before preview.',migrationRequest:JSON.stringify(template.request,null,2),migrationChoices:JSON.stringify(template,null,2),failed:false};
  }
  if(action==='migration-preview'||action==='migration-import'){
   const file=f.get('migrationFile');let raw=str('migrationRequest');
   if(action==='migration-preview'&&file instanceof File&&file.size){if(file.size>5_000_000)throw new Error('Use a normalized JSON file under 5 MB.');raw=await file.text();}
   if(!raw||raw.length>5_000_000)throw new Error('Provide normalized migration JSON under 5 MB.');
   const request=JSON.parse(raw);if(!request||Array.isArray(request)||typeof request!=='object')throw new Error('Migration JSON must contain one request object.');
   if(action==='migration-import'){
    if(f.get('confirmImport')!=='on')throw new Error('Confirm the reviewed source totals before import.');
    request.approvedFingerprint=str('approvedFingerprint');if(!request.approvedFingerprint)throw new Error('Run a clean preview before import.');
    await leadApi(event,'finance/migration/import',request);return{message:'Reviewed migration imported. Opening balances and source evidence are recorded; reconcile the reports before cutover.',failed:false};
   }
   const migrationPreview=await leadApi<FinanceMigrationPreview>(event,'finance/migration/preview',request);
   return{message:migrationPreview.canImport?'Preview controls reconcile. Review every warning and source total before importing.':'Preview found blocking errors. Correct the source mappings and controls.',migrationRequest:JSON.stringify(request,null,2),migrationPreview,failed:false};
  }
  if(action==='account')c.chartAccount={code:str('code'),name:str('name'),type:str('type'),normalBalance:str('normalBalance'),active:true};
  if(action==='period'){const [y,m]=str('month').split('-').map(Number);c.date=`${str('month')}-01`;c.endDate=new Date(Date.UTC(y,m,0)).toISOString().slice(0,10);}
  if(action==='budget-review')c.budget=Object.fromEntries([...f.entries()].filter(([k,v])=>k.startsWith('budget.')&&String(v)!=='').map(([k,v])=>[k.slice(7),Number(v)]));
  if(action==='cost-code')c.code={code:str('code'),name:str('name'),category:str('category'),trade:str('trade')||null};
  if(action==='cash-account')c.cashAccount={id:str('cashId'),name:str('name'),ledgerAccount:str('account'),institution:str('institution'),maskedReference:str('maskedReference'),currency:'USD'};
  if(action==='bill'){
   const descriptions=f.getAll('description').map(String),quantities=f.getAll('quantity').map(Number),prices=f.getAll('unitCost').map(Number);
   const accounts=f.getAll('lineAccount').map(String),jobs=f.getAll('lineJobId').map(String),codes=f.getAll('lineCostCode').map(String),cats=f.getAll('lineCategory').map(String),poLines=f.getAll('orderLineId').map(String),imported=f.getAll('importedCommitmentId').map(String);
   c.bill={id:str('billId')||crypto.randomUUID(),vendorId:str('partyId'),number:str('number'),date:str('date'),dueDate:str('dueDate'),orderId:str('orderId')||null,terms:str('terms'),tax:num('tax'),shipping:num('shipping'),retainagePercent:num('retainagePercent'),notes:str('reason'),lines:descriptions.map((description,i)=>({description,quantity:quantities[i],unitCost:prices[i],account:accounts[i],jobId:jobs[i]||null,costCode:codes[i]||null,category:cats[i]||'MATERIAL',orderLineId:poLines[i]||null,importedCommitmentId:imported[i]||null}))};
  }
  if(['post-journal','draft-journal','opening-import'].includes(action)){
   c.lines=csv(str('lines')).map(([account,debit,credit,description])=>({account,debit:Number(debit||0),credit:Number(credit||0),description:description||''}));
  }
  if(action==='allocate-bill-extras')c.lines=csv(str('lines')).map(([account,debit,credit,jobId,costCode,category])=>({account,debit:Number(debit||0),credit:Number(credit||0),jobId:jobId||null,costCode:costCode||null,category:category||null}));
  if(action==='opening-import'){
   c.arControl=num('arControl');c.apControl=num('apControl');c.importOpenItems=csv(str('openItems')).map(([id,kind,partyId,number,date,dueDate,total,externalId])=>({id,kind,partyId,number,date,dueDate,total:Number(total),externalId,journalId:'00000000-0000-0000-0000-000000000000'}));
  }
  if(['payment','refund','credit','vendor-payment','vendor-refund','vendor-credit'].includes(action))c.allocations=csv(str('allocations')).map(([openItemId,amount])=>({openItemId,amount:Number(amount)}));
  if(action==='bank-import')c.bankTransactions=csv(str('rows')).map(([externalId,date,amount,reference])=>({externalId,date,amount:Number(amount),reference:reference||'',cashAccountId:str('account'),importId:str('reference')}));
  if(action==='policy'){
   const w=await leadApi<FinanceWorkspace>(event,'finance');c.policy={...w.state.policy,matching:str('matching'),accounts:Object.fromEntries(Object.entries(w.state.policy.accounts).map(([role,code])=>[role,str(`map.${role}`)||code]))};
  }
  await leadApi(event,'finance/command',c);
  return {message:action==='sync-invoices'?'Invoice and payment events reconciled into Finance.':action==='draft-journal'?'Journal drafted. No ledger balances changed.':'Finance change recorded.',failed:false};
 }catch(e){const p=e as {status?:number;body?:{message?:string};message?:string};return fail(p.status||400,{message:p.body?.message||p.message||'The change is not confirmed. Refresh before retrying.',failed:true});}
}};
