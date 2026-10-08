export { customerName, customerInput, type CustomerRecord } from '../../../lib/customer-records.ts';
export const triageStatuses = (current: string) => {
 const next: Record<string, string[]> = {
  new: ['in-review', 'qualified', 'contacted', 'closed'], 'in-review': ['qualified', 'contacted', 'closed'],
  'needs-info': ['in-review', 'qualified', 'contacted', 'closed'], qualified: ['contacted', 'closed'],
  contacted: ['closed'], 'inspection-scheduled': ['closed'], 'estimate-drafted': ['closed'],
  'estimate-sent': ['closed'], won: [], closed: ['in-review']
 };
 return [current, ...(next[current] ?? [])];
};
