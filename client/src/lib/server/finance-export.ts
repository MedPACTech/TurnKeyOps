import {leadApi} from './leads';
import type {RequestHandler} from '@sveltejs/kit';
import type {FinanceWorkspace} from '$lib/finance';
import {financeCsvCell} from '../finance-csv';
export {financeCsvCell} from '../finance-csv';
export const GET:RequestHandler=async(event)=>{
 const params=new URLSearchParams();for(const key of ['from','asOf'])if(event.url.searchParams.has(key))params.set(key,event.url.searchParams.get(key)!);
 const w=await leadApi<FinanceWorkspace>(event,`finance?${params}`);const kind=event.url.searchParams.get('type');
 const rows:unknown[][]=kind==='aging'?[['Ledger','Document','Party ID','Due date','Days past due','Balance'],...w.reports.aging.map(a=>[a.kind,a.number,a.partyId,a.dueDate,a.daysPastDue,a.balance])]:kind==='income'?[['Account','Name','Type','Amount (credit less debit)'],...w.reports.incomeStatement.map(a=>[a.code,a.name,a.type,a.amount])]:[['Account','Name','Type','Debit','Credit','Net debit/(credit)'],...w.reports.trialBalance.map(a=>[a.code,a.name,a.type,a.debit,a.credit,a.balance])];
 return new Response(rows.map(r=>r.map(financeCsvCell).join(',')).join('\r\n'),{headers:{'Content-Type':'text/csv; charset=utf-8','Content-Disposition':'attachment; filename="finance-report.csv"','Cache-Control':'private, no-store','X-Content-Type-Options':'nosniff'}});
};
