import {fail,type RequestEvent} from '@sveltejs/kit';
import {leadApi} from './leads';
import {csv,type FinanceWorkspace} from '$lib/finance';
import type {ConstructionWorkspace} from '$lib/construction-finance';
export const load=async(event:RequestEvent)=>{
 event.setHeaders({'Cache-Control':'private, no-store'});
 const [workspace,construction,payments]=await Promise.all([leadApi<FinanceWorkspace>(event,'finance'),leadApi<ConstructionWorkspace>(event,'finance/construction'),leadApi<{version:string;configured:boolean;intents:{id:string;openItemId:string;amount:number;status:string;url?:string;error?:string}[];receipts:{eventId:string;status:string;error?:string}[]}>(event,'finance/construction/payments')]);
 return {workspace,construction,payments,basePath:event.url.pathname.split('/finance')[0]};
};
export const actions={save:async(event:RequestEvent)=>{
 const f=await event.request.formData();const str=(key:string)=>String(f.get(key)??'').trim();const num=(key:string)=>Number(str(key));
 try{
 const action=str('action');
 if(action.startsWith('payment-')){const operation=action.slice(8);if(!['create','retry','cancel','reconcile'].includes(operation))throw new Error('Unsupported payment operation.');await leadApi(event,'finance/construction/payments/command',{id:str('commandId')||crypto.randomUUID(),expectedVersion:str('version'),action:operation,openItemId:str('openItemId')||'00000000-0000-0000-0000-000000000000',targetId:str('target')||'00000000-0000-0000-0000-000000000000',cashAccountId:str('cashAccountId'),reason:str('reason')});return{message:'Construction payment operation recorded. Review provider status before retrying.',failed:false};}
 if(action==='bob'){
 const tool=str('tool');if(!['finance.construction-health','finance.construction-wip','finance.construction-retainage','finance.construction-backlog','finance.time-blockers','finance.vendor-compliance'].includes(tool))throw new Error('Choose a supported construction review.');
 const result=await leadApi<{resultJson:string}>(event,'finance/bob',{toolKey:tool,idempotencyKey:crypto.randomUUID(),input:{asOf:str('asOf')}});
 const raw=JSON.parse(result.resultJson||'null');const normalized=raw&&Object.fromEntries(Object.entries(raw).map(([k,v])=>[k[0].toLowerCase()+k.slice(1),v]));
 if(!normalized?.title||normalized.truncated)return{message:'Review exceeded the stored response limit. Open construction reports for complete records.',failed:false};
 const analysis={...normalized,findings:(normalized.findings as Record<string,unknown>[]??[]).map(f=>Object.fromEntries(Object.entries(f).map(([k,v])=>[k[0].toLowerCase()+k.slice(1),v])))};return{message:'Construction review recorded.',analysis,failed:false};
 }
 if(!['draft-code-budget','review-code-budget','code-commitment','void-application','policy','contract','forecast','application','submit-application','approve-application','invoice-application','release-retainage','calculate-wip','approve-wip','post-wip'].includes(action))throw new Error('Choose a supported construction finance action.');
 const command:Record<string,unknown>={id:str('commandId')||crypto.randomUUID(),expectedVersion:str('version'),action,jobId:str('jobId')||'00000000-0000-0000-0000-000000000000',target:str('target')||'00000000-0000-0000-0000-000000000000',date:str('date')||'0001-01-01',periodId:str('periodId'),reason:str('reason'),amount:num('amount')};
 if(action==='policy')command.accounts=Object.fromEntries(['retainage','contractAsset','contractLiability','billing'].map(role=>[role,str(`map.${role}`)]));
 if(action==='contract')command.contract={jobId:str('jobId'),customerId:str('customerId'),originalValue:num('originalValue'),approvedChanges:num('approvedChanges'),retainagePercent:num('retainagePercent'),recognitionMethod:str('recognitionMethod'),progressBillingEnabled:f.get('progressBillingEnabled')==='on',completed:f.get('completed')==='on',lines:csv(str('lines')).map(([id,description,originalValue,approvedChanges,costCode])=>({id,description,originalValue:Number(originalValue),approvedChanges:Number(approvedChanges||0),costCode:costCode||null}))};
 if(action==='forecast')command.forecast={id:crypto.randomUUID(),jobId:str('jobId'),date:str('date'),remainingForecast:num('remainingForecast'),complete:f.get('complete')==='on',reason:str('reason'),costs:str('forecastCosts')?csv(str('forecastCosts')).map(([category,costCode,remainingForecast])=>({category,costCode:costCode||null,remainingForecast:Number(remainingForecast)})):null};
 if(action==='draft-code-budget')command.codeBudget={id:crypto.randomUUID(),jobId:str('jobId'),date:str('date'),lines:csv(str('lines')).map(([category,costCode,amount])=>({category,costCode,amount:Number(amount)}))};
 if(action==='code-commitment')command.commitmentCoding={id:crypto.randomUUID(),jobId:str('jobId'),orderId:str('orderId')||'00000000-0000-0000-0000-000000000000',orderLineId:str('orderLineId')||'00000000-0000-0000-0000-000000000000',costCode:str('costCode'),date:str('date'),importedCommitmentId:str('importedCommitmentId')||null};
 if(action==='application')command.application={id:crypto.randomUUID(),jobId:str('jobId'),number:str('number'),periodStart:str('periodStart'),periodEnd:str('periodEnd'),documents:str('documents').split(',').map(s=>s.trim()).filter(Boolean),lines:csv(str('lines')).map(([sovLineId,currentBilling,newStoredMaterials,installedFromStored])=>({sovLineId,currentBilling:Number(currentBilling||0),newStoredMaterials:Number(newStoredMaterials||0),installedFromStored:Number(installedFromStored||0)}))};
 await leadApi(event,'finance/construction/command',command);
 return {message:'Construction finance change recorded. Review the refreshed source balances.',failed:false};
 }catch(e){const p=e as {status?:number;body?:{message?:string};message?:string};return fail(p.status||400,{message:p.body?.message||p.message||'Change is not confirmed. Refresh before retrying.',failed:true});}
}};
