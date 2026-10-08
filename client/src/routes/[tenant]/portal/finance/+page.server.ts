import {portalRequest} from '$lib/server/portal';
import type {PageServerLoad} from './$types';
export const load:PageServerLoad=async(event)=>{event.setHeaders({'Cache-Control':'private, no-store'});return{home:await portalRequest(event),invoices:await portalRequest<{id:string;number:string;date:string;dueDate:string;total:number;balance:number;paymentUrl:string|null;receipts:{date:string;kind:string;amount:number;reference:string}[]}[]>(event,'/finance')};};
