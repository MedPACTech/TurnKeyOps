import {fail,type RequestEvent} from '@sveltejs/kit';
import {leadApi} from './leads';
import type {JobsPage,JobDetail} from '$lib/jobs';
export const load=async(event:RequestEvent)=>{
 event.setHeaders({'Cache-Control':'private, no-store'});
 const workspace=await leadApi<JobsPage['workspace']>(event,'job-workspace');const id=event.params.id||event.url.searchParams.get('job');
 const choices=await leadApi<JobsPage['choices']>(event,'job-workspace/choices');
 const notifications=id?await leadApi<JobsPage['notifications']>(event,`job-workspace/${encodeURIComponent(id)}/notifications`):[];
 return {workspace,choices,notifications,fieldView:event.locals.adminSession?.role==='estimator-crew-lite',selected:id?await leadApi<JobDetail>(event,`job-workspace/${encodeURIComponent(id)}`):null,basePath:event.url.pathname.split('/jobs')[0]+'/jobs'};
};
export const actions={save:async(event:RequestEvent)=>{
 const f=await event.request.formData();const str=(k:string)=>String(f.get(k)||'');const id=str('id'),action=str('action'),expectedVersion=str('version');const path=`job-workspace/${encodeURIComponent(id)}`;
 try{
  if(action==='create'){const created=await leadApi<JobDetail>(event,'job-workspace',{name:str('name'),customerId:str('customerId'),jobSiteId:str('jobSiteId'),tradeProfile:str('tradeProfile'),serviceType:str('serviceType'),origin:str('origin'),sourceSystem:str('sourceSystem'),externalId:str('externalId'),scope:str('scope')});return{message:'Job created.',createdId:created.job.id};}
  if(action==='files'){const files=new FormData();files.set('expectedVersion',expectedVersion);files.set('purpose',str('purpose')||'field');for(const file of f.getAll('files'))files.append('files',file);await leadApi(event,`${path}/files`,files);}
  else if(action==='schedule'){await leadApi(event,`${path}/events`,{expectedVersion,id:str('eventId')||undefined,status:str('status')||'scheduled',title:str('title'),type:str('type')||'work',customerVisible:str('customerVisible')==='true',startUtc:str('startUtc'),endUtc:str('endUtc'),membershipIds:f.getAll('membershipId').map(String).filter(Boolean),resourceIds:str('resources').split(',').map(s=>s.trim()).filter(Boolean),notes:str('text')});}
  else if(action==='recommend'){const recommendations=await leadApi<{membershipId:string;eligible:boolean;explanation:string}[]>(event,`${path}/recommendations?start=${encodeURIComponent(str('startUtc'))}&end=${encodeURIComponent(str('endUtc'))}`);return{message:'Assignment recommendations ready.',recommendations};}
  else if(action==='bob'){const toolKey=str('tool');const bob=await leadApi<{id:string;status:string;confirmationRequired:boolean;resultJson:string}>(event,`${path}/bob`,{toolKey,input:{jobId:id,expectedVersion,text:str('text'),channel:str('channel'),template:str('template')},idempotencyKey:crypto.randomUUID()});return{message:bob.confirmationRequired?'Bob needs approval.':toolKey==='job.notify'?'Notification processed. Check customer notification receipts for its delivery status.':'Bob finished the requested action.',bob};}
  else if(action==='approve-bob')await leadApi(event,`${path}/bob/${encodeURIComponent(str('actionId'))}/approve`,{});
  else{
   const command:Record<string,unknown>={expectedVersion,action,text:str('text'),state:str('state'),itemId:str('itemId')||null,evidenceIds:f.getAll('evidenceId').map(String),ownerMembershipId:str('ownerMembershipId')||null};
   if(action==='trade-data')command.tradeData=Object.fromEntries([...f.entries()].filter(([k])=>k.startsWith('field.')).map(([k,v])=>[k.slice(6),String(v)]));
   if(action==='issue')command.issue={description:str('text'),type:str('type'),severity:str('severity')};
   if(action==='change')command.change={description:str('text'),reason:str('reason'),scopeImpact:str('scopeImpact'),scheduleImpact:str('scheduleImpact'),pricingImpact:f.get('pricingImpact')==='on'};
   if(action==='change-status')command.change={acceptedEstimateId:str('acceptedEstimateId')||null};
   if(action==='add-task')command.task={title:str('title'),stage:str('stage'),required:true};
   if(action==='requirement'){const previous=str('itemId')?(await leadApi<JobDetail>(event,path)).job.execution?.requirements.find(r=>r.id===str('itemId')):null;command.requirement={...previous,description:str('description'),kind:str('kind'),quantity:Number(str('quantity')),unit:str('unit'),status:str('status'),notes:str('text')||previous?.notes||''};}
   if(action==='accept')command.acceptance={signer:str('signer'),statement:str('statement'),signature:str('signature'),exceptions:str('exceptions')};
   await leadApi(event,`${path}/command`,command);
  }
  return{message:'Job updated.'};
 }catch(e){const p=e as {status?:number;body?:{message?:string};message?:string};return fail(p.status||400,{message:p.body?.message||p.message||'The change is not confirmed. Refresh before retrying.',failed:true});}
}};
