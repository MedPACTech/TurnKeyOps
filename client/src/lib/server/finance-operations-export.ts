import {error,type RequestHandler} from '@sveltejs/kit';
import {leadApi} from './leads';import {financeCsvCell} from './finance-export';
export const GET:RequestHandler=async(event)=>{
 const year=event.url.searchParams.get('taxYear')||String(new Date().getUTCFullYear());if(!/^20\d{2}$/.test(year))error(400,'Choose a valid tax year.');
 const w=await leadApi<{vendorPayments1099:{vendorId:string;payments:{id:string;date:string;amount:number;reference:string;journalId:string}[]}[];payrollExport:{id:string;employeeId:string;jobId:string;date:string;hours:number;earningsClass:string;baseRateSnapshot:number;burdenRateSnapshot:number;journalId:string}[]}>(event,`finance/operations?taxYear=${year}`);
 const kind=event.url.searchParams.get('type');let rows:unknown[][];
 if(kind==='1099')rows=[['Vendor ID','Payment ID','Date','Amount','Reference','Journal ID'],...w.vendorPayments1099.flatMap(v=>v.payments.map(p=>[v.vendorId,p.id,p.date,p.amount,p.reference,p.journalId]))];
 else if(kind==='payroll')rows=[['Time ID','Employee ID','Job ID','Work date','Hours','Earnings class','Base cost rate snapshot','Burden rate snapshot','Journal ID'],...w.payrollExport.map(t=>[t.id,t.employeeId,t.jobId,t.date,t.hours,t.earningsClass,t.baseRateSnapshot,t.burdenRateSnapshot,t.journalId])];else error(400,'Unsupported operations export.');
 return new Response(rows.map(row=>row.map(financeCsvCell).join(',')).join('\r\n'),{headers:{'Content-Type':'text/csv; charset=utf-8','Content-Disposition':`attachment; filename="finance-${kind}-${year}.csv"`,'Cache-Control':'private, no-store','X-Content-Type-Options':'nosniff'}});
};
