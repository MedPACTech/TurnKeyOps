import { error, fail, redirect, type RequestEvent } from '@sveltejs/kit';
import { peopleRequest } from './people';
export type Contact = {id:string;firstName:string;lastName:string;contactEmail?:string;contactPhone?:string;companyName?:string;profileTypes:string[];customerId?:string;address?:string;city?:string;state?:string;postalCode?:string;notes?:string;version:string};
export type ContactWork = {jobs:{id:string;name:string;status:string;address?:string}[];invoices:{id:string;number:string;status:string}[];canViewJobs:boolean;canViewInvoices:boolean};
export const loadContacts = async (event:RequestEvent) => {
 const [contacts,customerLinks] = await Promise.all([
  peopleRequest<Contact[]>(event,'/api/contacts'), peopleRequest<{id:string;name:string;companyName?:string}[]>(event,'/api/contacts/customers')
 ]);
 const selectedId=event.url.searchParams.get('person');
 if(selectedId && !contacts.some(c=>c.id===selectedId))throw error(404,'Contact not found in this company.');
 const work=selectedId ? await peopleRequest<ContactWork>(event,`/api/contacts/${encodeURIComponent(selectedId)}/work`) : null;
 return {contacts,customerLinks,selectedId,work,contactSaved:event.url.searchParams.get('saved')==='1'};
};
const text=(data:FormData,key:string)=>String(data.get(key)??'').trim();
export const contactActions = {
 saveContact:async(event:RequestEvent)=>{
  const data=await event.request.formData(); const id=text(data,'id');
  let saved:Contact;
  try {
   saved=await peopleRequest<Contact>(event,`/api/contacts${id?'/'+encodeURIComponent(id):''}`,{method:id?'PUT':'POST',body:JSON.stringify({
    firstName:text(data,'firstName'),lastName:text(data,'lastName'),contactEmail:text(data,'contactEmail')||null,contactPhone:text(data,'contactPhone')||null,
    companyName:text(data,'companyName'),profileTypes:data.getAll('profileTypes').map(String),customerId:text(data,'customerId')||null,
    address:text(data,'address'),city:text(data,'city'),state:text(data,'state'),postalCode:text(data,'postalCode'),notes:text(data,'notes'),expectedVersion:text(data,'version')
   })});
  }catch(cause){const c=cause as {status?:number;body?:{message?:string};message?:string};return fail(c.status&&c.status>=400&&c.status<=599?c.status:400,{error:c.body?.message||c.message||'Could not save contact.'});}
  throw redirect(303,`${event.url.pathname}?person=${encodeURIComponent(saved.id)}&saved=1`);
 }
};
