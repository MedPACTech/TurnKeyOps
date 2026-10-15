export type BobSource = {module:string;entityId:string;condition:string;version:string};
export type BobFact = {source:BobSource;title:string;explanation:string;nextAction:string;severity:number;dueUtc?:string;capabilityId?:string;trade:string};
export type AttentionItem = {id:string;source:BobSource;severity:number;title:string;explanation:string;recommendedAction:string;status:string;escalatedTo?:string;dueUtc?:string};
export type Workflow = {id:string;goal:string;status:string;failure?:string;createdUtc:string;updatedUtc:string;steps:{capabilityId:string;status:string;rationale:string;conversationId?:string;input?:Record<string,unknown>;actionId?:string;failure?:string}[]};
export type Briefing = {context:{role:string;asOfUtc:string;modules:string[];unavailableModules:string[];incompleteModules?:string[];facts:BobFact[]};priorities:AttentionItem[];atRisk:AttentionItem[];needsDecision:Workflow[];handled:Workflow[];watching:Workflow[];nextActions:{action:string;rationale:string;source:BobSource;capabilityId?:string;canExecute:boolean;blockingRequirements:string[]}[]};
export type BobPolicy = {enabled:boolean;proactiveEnabled:boolean;enabledModules:string[];actions:Record<string,number>;escalations:{module:string;condition:string;afterHours:number;team:string;severity:number}[];roleModules:Record<string,string[]>;externalNotificationsEnabled:boolean;customerBobEnabled:boolean};
export type BobCapability = {id:string;module:string;risk:number;writePermissions:string[];requiredInputs:string[];isDraft:boolean};
export type BobRule = {id:string;name:string;enabled:boolean;module:string;condition:string;minimumSeverity:number;action:string;priority:number;ownerUserId:string};
export type AutomationCenter = {version:string;policy:BobPolicy;rules:BobRule[];capabilities:BobCapability[];suggestions:{id:string;title:string;description:string}[]};
export type BobActivity = {workflows:Workflow[];events:{id:string;action:string;rationale:string;atUtc:string}[];metrics:{completed:number;waitingApproval:number;failed:number;attentionResolved:number;feedback:number}};
export type BobCommand = {intent:string;status:string;message:string;matches:BobFact[];actions:{action:string;rationale:string}[]};
export const severityLabels = ['Info','Action','Warning','Critical'];
export const autonomyLabels = ['Disabled','Recommend only','Draft','Act with approval','Auto act by policy'];
export const riskLabels = ['Low','Medium','High','Explicit human'];
export const sourceHref = (base:string, source:BobSource) => {
 const module=source.module==='calendar'?'calendar':source.module;
 if(module==='leads')return `${base}/leads/${encodeURIComponent(source.entityId)}`;
 return `${base}/${module}?${module==='jobs'?'job':module==='estimates'?'request':'record'}=${encodeURIComponent(source.entityId)}`;
};
