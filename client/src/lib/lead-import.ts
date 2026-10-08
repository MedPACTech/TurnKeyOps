import {leadSources} from './leads.ts';
/** Provider-neutral preview mapping: CSV/Excel/CRM adapters provide rows; an operator reviews before creation. */
export type LeadImportMapping = {title:string;contactName?:string;email?:string;phone?:string;siteAddress?:string;requestedWork?:string;source?:string;externalId?:string};
export function previewLeadImport(rows:Record<string,string>[],mapping:LeadImportMapping,provider:string) {
 if(!provider.trim()) throw new Error('An import provider is required.');
 if(rows.length>1000) throw new Error('Preview at most 1000 rows per batch.');
 return rows.map((row,index)=>{
  const value=(key:keyof LeadImportMapping)=>mapping[key]?String(row[mapping[key]!]??'').trim():'';
  const source=value('source')||'Other';const title=value('title');
  const problems=[...(!title?['Opportunity name is missing']:[]),...(!leadSources.includes(source)?['Unknown lead source']:[])];
  return {row:index+1,problems,lead:{title,contactName:value('contactName'),email:value('email'),phone:value('phone'),siteAddress:value('siteAddress'),requestedWork:value('requestedWork'),source,attribution:{importProvider:provider,externalId:value('externalId')}},requiresReview:true as const};
 });
}
