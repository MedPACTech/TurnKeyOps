export type FinanceAccount={code:string;name:string;type:string;normalBalance:string;systemControlled:boolean;active:boolean};
export type FinanceLine={account:string;debit:number;credit:number;jobId?:string;costCode?:string;description?:string};
export type FinanceWorkspace={canWrite:boolean;canControl:boolean;jobChoices:{id:string;name:string}[];vendors:{id:string;name:string}[];orders:{id:string;number:string;vendorId:string;lines:{id:string;description:string;quantity:number;unitCost:number}[]}[];
 state:{version:string;accounts:FinanceAccount[];periods:{id:string;start:string;end:string;status:string}[];journals:{id:string;date:string;sourceType:string;sourceId:string;memo:string;postedBy:string;postedAtUtc:string;reversesId?:string;lines:FinanceLine[]}[];journalDrafts:{id:string;date:string;memo:string;postedJournalId?:string;lines:FinanceLine[]}[];
 openItems:{id:string;kind:string;partyId:string;number:string;date:string;dueDate:string;total:number;journalId:string;jobId?:string}[];
 settlements:{id:string;kind:string;partyId:string;date:string;amount:number;reference:string;sourceKey:string;journalId:string;allocations:{openItemId:string;amount:number}[]}[];
 bills:{id:string;vendorId:string;number:string;date:string;dueDate:string;total:number;status:string;matchExceptions:string[];journalId?:string}[];
 cashAccounts:{id:string;name:string;ledgerAccount:string;institution:string;maskedReference:string}[];
 bankTransactions:{id:string;date:string;cashAccountId:string;amount:number;reference:string;externalId:string;journalId?:string;reconciliationId?:string}[];
 reconciliations:{id:string;cashAccountId:string;start:string;end:string;openingBalance:number;closingBalance:number}[];
 costCodes:{code:string;name:string;category:string;trade?:string}[];costs:{id:string;jobId:string;category:string;amount:number;date:string;sourceType:string;sourceId:string;journalId?:string}[];
 audit:{commandId:string;action:string;target:string;actor:string;atUtc:string;reason:string}[];policy:{matching:string;accounts:Record<string,string>;currency:string;materialActualSource:string;revenueRecognition:string}};
 reports:{from:string;asOf:string;ar:number;ap:number;arDifference:number;apDifference:number;trialDifference:number;profit:number;balanceSheet:{assets:number;liabilities:number;equity:number;currentEarnings:number;difference:number};
 trialBalance:{code:string;name:string;type:string;debit:number;credit:number;balance:number}[];incomeStatement:{code:string;name:string;type:string;amount:number}[];
 aging:{id:string;kind:string;number:string;partyId:string;jobId?:string;dueDate:string;balance:number;daysPastDue:number;bucket:string}[];cash:{id:string;name:string;balance:number}[]};
 jobs:{jobId:string;contractValue:number;approvedChanges:number;currentValue:number;budget:Record<string,number|null>;committed:number;actual:number;revenue:number;grossProfit:number|null;grossMargin:number|null;costDataIncomplete:boolean;completenessNote:string;categories:{category:string;amount:number}[];sourceIds:{sourceType:string;sourceId:string;journalId?:string}[]}[];
 close:{id:string;issues:string[]}[];suggestions:{id:string;candidates:{id:string;date:string;memo:string;sourceType:string;sourceId:string;confidence:string}[]}[]};
export const money=(value:number|null|undefined)=>value==null?'Unknown':new Intl.NumberFormat('en-US',{style:'currency',currency:'USD'}).format(value);
export const categories=['MATERIAL','LABOR','EQUIPMENT','SUBCONTRACT','OTHER_DIRECT_COST','BURDEN','OVERHEAD_ALLOCATED'];
// CSV imports are deliberately strict; exported systems must quote delimiters and embedded newlines.
export function csv(text:string):string[][]{
 const rows:string[][]=[];let row:string[]=[],cell='',quoted=false;
 for(let i=0;i<text.length;i++){const c=text[i];if(c==='"'){if(quoted&&text[i+1]==='"'){cell+='"';i++;}else if(quoted||cell==='')quoted=!quoted;else throw new Error('Invalid CSV quote.');}else if(c===','&&!quoted){row.push(cell.trim());cell='';}else if((c==='\n'||c==='\r')&&!quoted){if(c==='\r'&&text[i+1]==='\n')i++;row.push(cell.trim());if(row.some(Boolean))rows.push(row);row=[];cell='';}else cell+=c;}
 if(quoted)throw new Error('Unclosed CSV quote.');row.push(cell.trim());if(row.some(Boolean))rows.push(row);return rows;
}
