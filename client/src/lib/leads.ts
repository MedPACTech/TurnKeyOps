export const leadStages = ['NEW', 'NEEDS_RESPONSE', 'QUALIFYING', 'QUALIFIED', 'DISCOVERY', 'READY_TO_ESTIMATE', 'ESTIMATING', 'PROPOSAL', 'FOLLOW_UP', 'WON', 'LOST'] as const;
export const leadSources = ['Website', 'Referral', 'Phone', 'Manual', 'Existing Customer', 'Walk-in', 'Other'];
export type Lead = {
 id: string; tenantId: string; title: string; customerId: string | null; contactName: string; companyName: string; email: string; phone: string;
 siteAddress: string; requestedWork: string; tradeProfile: string; service: string; propertyType: string; source: string;
 ownerMembershipId: string | null; ownerName: string; estimatedValue: number | null; referralContactId: string | null; referralName: string;
 nextAction: string; followUpAtUtc: string | null; qualification: Record<string,string>; attribution: Record<string,string>;
 stage: string; stageLabel: string; closeReason: string; estimateId: string | null; jobId: string | null; intakeRequestId: string | null;
 createdAtUtc: string; updatedAtUtc: string; version: string; bobSummary: string; missingRequired: string[];
 fields: {key: string; label: string; required: boolean; value: string}[];
 activity: {id: string; type: string; actor: string; text: string; occurredAtUtc: string; relatedId: string | null}[];
};
export type LeadConfiguration = {
 tradeProfiles: string[]; defaultTradeProfile: string; stageLabels: Record<string,string>; requiredFields: Record<string,Record<string,string>>;
 wonReasons: string[]; lostReasons: string[]; referralRequired: boolean; assignmentMode: string;
 assignmentRules: {membershipId:string; trade:string; service:string; propertyType:string; source:string; territory:string; priority:number}[];
 aiActions: Record<string,string>;
};
export type LeadWorkspace = {leads: Lead[]; configuration: LeadConfiguration; members: {id:string; name:string}[]; canWrite: boolean; canConfigure: boolean};
export type Duplicate = {id:string; kind:string; name:string; match:string};
export const queueFor = (stage: string) => ({NEW:'Needs response', NEEDS_RESPONSE:'Needs response', QUALIFYING:'Ready to qualify', QUALIFIED:'Discovery / site visit', DISCOVERY:'Discovery / site visit', READY_TO_ESTIMATE:'Ready to estimate', ESTIMATING:'Ready to estimate', PROPOSAL:'Waiting on customer', FOLLOW_UP:'Follow up', WON:'Won', LOST:'Lost'}[stage] ?? 'Needs response');
export const stageLabel = (stage:string, config:LeadConfiguration) => config.stageLabels[stage] || stage.toLowerCase().replaceAll('_',' ');
