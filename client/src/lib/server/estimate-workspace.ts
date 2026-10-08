import {fail,redirect,type RequestEvent} from '@sveltejs/kit';
import {leadApi} from './leads';
import type {EstimateList,EstimateWorkspace} from '$lib/estimates';
export const load=async(event:RequestEvent)=>{
 event.setHeaders({'Cache-Control':'private, no-store'});
 const workspace=await leadApi<EstimateList>(event,'estimate-workspace');
 const basePath=event.url.pathname.split('/estimates')[0]+'/estimates';
 const id=event.params.id||event.url.searchParams.get('request');
 if(id&&workspace.packets.some(p=>p.id===id&&!p.modern))redirect(303,`${basePath}/legacy?request=${encodeURIComponent(id)}`);
 const selected=id?await leadApi<EstimateWorkspace>(event,`estimate-workspace/${encodeURIComponent(id)}`):null;
 return {workspace,selected,basePath};
};
export const actions={save:async(event:RequestEvent)=>{
 const form=await event.request.formData();const id=String(form.get('id')||'');const action=String(form.get('action')||'');const expectedVersion=String(form.get('version')||'');
 try{
  const path=`estimate-workspace/${encodeURIComponent(id)}`;
  if(action==='files'){const uploads=new FormData();uploads.set('expectedVersion',expectedVersion);for(const file of form.getAll('files'))uploads.append('files',file);await leadApi(event,`${path}/files`,uploads);}
  else if(action==='price')await leadApi(event,path,{expectedVersion,document:JSON.parse(String(form.get('document'))),discountPercent:Number(form.get('discount')||0)},'PUT');
  else if(action==='bob'){
   const toolKey=String(form.get('tool'));if(!['estimate.extract','estimate.price','estimate.summarize','estimate.revise','estimate.issue','estimate.remind'].includes(toolKey))throw new Error('Unsupported action');
   const bob=await leadApi<{id:string;status:string;confirmationRequired:boolean}>(event,`${path}/bob`,{toolKey,input:{estimateId:id,expectedVersion,text:String(form.get('text')||'')},idempotencyKey:crypto.randomUUID()});
   return {message:'Bob action recorded.',bob};
  }else if(action==='approve-bob')await leadApi(event,`${path}/bob/${encodeURIComponent(String(form.get('actionId')))}/approve`,{});
  else if(['approve','issue','revision','void','deliver','job'].includes(action))await leadApi(event,`${path}/${action}`,{expectedVersion,text:String(form.get('text')||''),channel:String(form.get('channel')||'email')});
  else throw new Error('Unsupported action');
  return {message:action==='job'?'Accepted scope linked to Job.':'Estimate updated.'};
 }catch(e){return fail(400,{message:(e as {body?:{message?:string}}).body?.message||(e instanceof Error?e.message:'The estimate could not be updated.'),failed:true});}
}};
