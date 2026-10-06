import { fail, type RequestEvent } from '@sveltejs/kit';
import { leadApi } from './leads';
import { leadStages, type LeadWorkspace } from '$lib/leads';
type Settings = {values:Record<string,unknown>; version:string; schemaVersion:number};
export const load = async (event:RequestEvent) => {
 const workspace = await leadApi<LeadWorkspace>(event,'leads');
 const settings = await leadApi<Settings>(event,'admin/tenant-settings/operational');
 return {workspace,version:settings.version,basePath:event.url.pathname.replace('/settings','')};
};
export const actions = { save: async (event:RequestEvent) => {
 const form=await event.request.formData();
 try {
  
  const workspace=await leadApi<LeadWorkspace>(event,'leads');
  const config={...workspace.configuration,stageLabels:Object.fromEntries(leadStages.map(stage=>[stage,String(form.get(`stage.${stage}`)||stage.toLowerCase().replaceAll('_',' '))])),
   tradeProfiles:String(form.get('tradeProfiles')).split(',').map(s=>s.trim()).filter(Boolean),defaultTradeProfile:String(form.get('defaultTradeProfile')),
   wonReasons:String(form.get('wonReasons')).split('\n').map(s=>s.trim()).filter(Boolean),lostReasons:String(form.get('lostReasons')).split('\n').map(s=>s.trim()).filter(Boolean),
   referralRequired:form.get('referralRequired')==='on',assignmentMode:String(form.get('assignmentMode')),
   requiredFields:JSON.parse(String(form.get('requiredFields'))),assignmentRules:JSON.parse(String(form.get('assignmentRules'))),
   aiActions:Object.fromEntries(['lead.summarize','lead.assign','lead.stage','lead.task','lead.estimate','lead.draft','lead.send','lead.schedule'].map(key=>[key,String(form.get(key))]))};
  await leadApi(event,'leads/configuration',{expectedVersion:String(form.get('version')),configuration:config},'PUT');
  return {success:true,message:'Lead workflow settings saved.'};
 } catch(e) {return fail(400,{success:false,message:(e as {body?:{message?:string}}).body?.message || 'Settings could not be saved. Check the configuration and refresh if someone else edited it.'});}
}};
