import {fail,type RequestEvent} from '@sveltejs/kit';
import {leadApi} from './leads';
import type {SupplyWorkspace,SupplyPage} from '$lib/supply';
export const load=async(event:RequestEvent):Promise<SupplyPage>=>{
 event.setHeaders({'Cache-Control':'private, no-store'});
 const module=event.url.pathname.includes('/purchasing')?'purchasing':'inventory';
 return {workspace:await leadApi<SupplyWorkspace>(event,module),module,basePath:event.url.pathname.split(`/${module}`)[0],jobId:event.url.searchParams.get('job')};
};
export const actions={save:async(event:RequestEvent)=>{
 const f=await event.request.formData();const str=(k:string)=>String(f.get(k)||'');const num=(k:string)=>Number(str(k));
 const action=str('action');const command:Record<string,unknown>={id:str('commandId')||crypto.randomUUID(),expectedVersion:str('version'),action,targetId:str('targetId')||'00000000-0000-0000-0000-000000000000',itemId:str('itemId'),locationId:str('locationId')||null,toLocationId:str('toLocationId')||null,quantity:num('quantity'),damaged:num('damaged'),unit:str('unit')||'each',state:str('state'),reason:str('reason'),reference:str('reference'),expectedAtUtc:str('expectedAtUtc')||null};
 try{
  if(action==='bob'){const module=event.url.pathname.includes('/purchasing')?'purchasing':'inventory';const result=await leadApi<{confirmationRequired:boolean}>(event,`${module}/bob`,{toolKey:module==='purchasing'?'supply.purchasing':'supply.shortages',idempotencyKey:crypto.randomUUID(),input:{}});return{message:result.confirmationRequired?'Bob needs approval.':'Bob reviewed current supply. Shortages, incoming orders and source recommendations reflect recorded data below.',failed:false};}
  if(action==='send-order'){await leadApi(event,`purchasing/${encodeURIComponent(str('targetId'))}/send`,{expectedVersion:str('version')});return{message:'Vendor send processed. Check the order send status; provider acceptance does not confirm vendor delivery.',failed:false};}
  if(action==='files'){const module=str('fileModule')==='purchasing'?'purchasing':'inventory';const files=new FormData();files.set('expectedVersion',str('version'));for(const file of f.getAll('files'))files.append('uploads',file);await leadApi(event,`${module}/${encodeURIComponent(str('targetId'))}/files`,files);return{message:'Files attached.',failed:false};}
  if(action==='schedule-delivery'){
   const job=await leadApi<{job:{version:string}}>(event,`job-workspace/${encodeURIComponent(str('jobId'))}`);const reference=`supply-demand:${str('targetId')}:${crypto.randomUUID()}`;
   const created=await leadApi<{events:{id:string;description:string}[]}>(event,`job-workspace/${encodeURIComponent(str('jobId'))}/events`,{expectedVersion:job.job.version,type:'delivery',title:str('title'),startUtc:new Date(str('startUtc')).toISOString(),endUtc:new Date(str('endUtc')).toISOString(),membershipIds:[],resourceIds:[],status:'scheduled',notes:reference});
   const eventId=created.events.find(e=>e.description===reference)?.id;if(!eventId)throw new Error('Delivery was submitted but its Calendar identity needs review in Jobs.');
   await leadApi(event,'inventory/command',{...command,action:'link-delivery',reference:eventId});return{message:'Delivery scheduled in the shared Calendar.',failed:false};
  }

  if(action==='catalog')command.item={id:str('catalogId')||crypto.randomUUID(),name:str('name'),description:str('description'),kind:str('kind'),unit:str('unit'),sku:str('sku'),stocked:f.get('stocked')==='on',jobSpecific:f.get('jobSpecific')==='on',trades:str('trade')?[str('trade')]:[],metadata:Object.fromEntries([...f.entries()].filter(([key])=>key.startsWith('meta.')).map(([key,value])=>[key.slice(5),String(value)])),unitConversions:str('conversionUnit')?{[str('conversionUnit')]:num('conversionFactor')}:{},sourceSystem:str('sourceSystem')||'manual',externalId:str('externalId')};
  if(action==='location')command.location={name:str('name'),type:str('type'),address:str('address')};
  if(action==='vendor')command.vendor={contactId:str('contactId'),preferred:f.get('preferred')==='on',orderingEmail:str('orderingEmail'),leadDays:num('leadDays'),delivers:f.get('delivers')==='on'};
  if(action==='offer')command.offer={itemId:str('itemId'),vendorId:str('vendorId'),unit:str('unit'),cost:num('cost'),leadDays:num('leadDays'),packSize:num('packSize')||1,preferred:f.get('preferred')==='on',vendorItemNumber:str('vendorItemNumber')};
  if(action==='demand')command.demand={jobId:str('jobId'),requirementId:str('requirementId'),itemId:str('itemId')||null,strategy:str('strategy')||''};
  if(action==='draft-order')command.order={vendorId:str('vendorId'),locationId:str('locationId')||null,expectedAtUtc:str('expectedAtUtc')||null,deliveryInstructions:str('instructions'),tax:num('tax'),shipping:num('shipping'),lines:[{demandId:str('targetId'),requestId:str('requestId')||null,quantity:num('quantity'),unit:str('unit'),unitCost:num('cost'),directToJob:f.get('directToJob')==='on'}]};
  if(action==='policy'){
   const workspace=await leadApi<SupplyWorkspace>(event,'purchasing');if(!workspace.policy)throw new Error('Owner permission is required.');const policy=workspace.policy;
   policy.approvalAbove=num('approvalAbove');policy.nonPreferredApproval=f.get('nonPreferredApproval')==='on';policy.rushApproval=f.get('rushApproval')==='on';policy.priceOverrideApproval=f.get('priceOverrideApproval')==='on';
   for(const [trade,profile] of Object.entries(policy.trades)){profile.gate=str(`gate.${trade}`)||profile.gate;profile.defaultStrategy=str(`strategy.${trade}`)||profile.defaultStrategy;profile.deliveryRequired=f.get(`delivery.${trade}`)==='on';}
   for(const key of ['supply.availability','supply.shortages','supply.sources','supply.purchasing','supply.reserve','supply.request','supply.draft-order','supply.send'])if(str(key))policy.aiActions[key]=str(key);
   command.policy=policy;
  }
  const module=['vendor','offer','request','approve-request','draft-order','order-status','policy'].includes(action)?'purchasing':'inventory';
  await leadApi(event,`${module}/command`,command);
  return{message:action==='order-status'&&str('state')==='SENT'?'External vendor send recorded. No email was sent by this form.':'Supply updated.',failed:false};
 }catch(e){const p=e as {status?:number;body?:{message?:string};message?:string};return fail(p.status||400,{message:p.body?.message||p.message||'The change is not confirmed. Refresh before retrying.',failed:true});}
}};
