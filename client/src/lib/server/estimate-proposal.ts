import {error,fail,type RequestEvent,type ActionFailure} from '@sveltejs/kit';
import {getTurnKeyApiBaseUrl,unwrapTurnKeyApiEnvelope} from './turnkey-api';
import {getPublicQuoteEstimate} from './quote-estimates';
import {parseQuoteSignatureInput,type QuoteApprovalSignature} from '$lib/quote-signatures';
const slug=(event:RequestEvent)=>event.url.pathname.split('/')[1];
export const load=async(event:RequestEvent)=>{
 event.setHeaders({'Cache-Control':'private, no-store','Referrer-Policy':'no-referrer','X-Robots-Tag':'noindex, nofollow'});
 const token=event.url.searchParams.get('token')?.trim()||'';if(!token)throw error(404,'Proposal link is unavailable.');
 try{const draft=await getPublicQuoteEstimate(event.fetch,slug(event),event.params.requestId!,token);return {draft,accessToken:token,quoteRequest:{email:draft.delivery?.email??'',phone:draft.delivery?.phone??''},returnTo:'',proposalBase:event.url.pathname};}
 catch{throw error(404,'This proposal link is unavailable or expired. Ask the office for a current link.');}
};
type ProposalActionResult={approvalError?:string;changeMessage?:string;decisionError?:string;signerPrintedName?:string;responseNote?:string;approved?:boolean;changesRequested?:boolean;approvalSignature?:QuoteApprovalSignature;message?:string};
const decide=async(event:RequestEvent,approve:boolean):Promise<ProposalActionResult|ActionFailure<ProposalActionResult>>=>{
 const form=await event.request.formData();const signature=parseQuoteSignatureInput(form);
 if(approve&&!signature.signature)return fail(400,{approvalError:signature.error,signerPrintedName:signature.signerPrintedName});
 const note=String(form.get('responseNote')||'').trim();if(!approve&&!note)return fail(400,{changeMessage:'Add a note for the office.'});
 try{
 const result=await event.fetch(`${getTurnKeyApiBaseUrl()}/api/public/quote-estimates/${slug(event)}/${event.params.requestId}/${approve?'approve':'request-changes'}`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({accessToken:String(form.get('accessToken')||''),responseNote:note,revisionNumber:Number(form.get('revisionNumber')),documentHash:String(form.get('documentHash')||''),...(approve?signature.signature:{}),selectedOptionIds:form.getAll('option').map(String),decline:form.get('decline')==='yes'})});
 const saved=await unwrapTurnKeyApiEnvelope<{approvalSignature:QuoteApprovalSignature}>(result,'Record proposal decision');return {approved:approve,changesRequested:!approve,approvalSignature:saved.approvalSignature,message:approve?'Your signed approval was recorded.':'Your response was recorded.'};
 }catch{return fail(409,{responseNote:note,decisionError:'Your response was not confirmed. Reload and try again.',approvalError:'Your response was not confirmed. Reload to check the current revision before retrying.',changeMessage:'The response was not confirmed. Reload and retry.'});}
};
export const actions={approve:(event:RequestEvent)=>decide(event,true),requestChanges:(event:RequestEvent)=>decide(event,false)};
export const download=async(event:RequestEvent)=>{
 const token=event.url.searchParams.get('token')||'';
 const response=await event.fetch(`${getTurnKeyApiBaseUrl()}/api/public/quote-estimates/${slug(event)}/${event.params.requestId}/files/${event.params.fileId}?token=${encodeURIComponent(token)}`);
 if(!response.ok)throw error(404,'Proposal file unavailable.');
 return new Response(response.body,{headers:{'Content-Type':response.headers.get('content-type')||'application/octet-stream','Content-Disposition':response.headers.get('content-disposition')||'attachment','Cache-Control':'private, no-store','Referrer-Policy':'no-referrer','X-Content-Type-Options':'nosniff'}});
};
