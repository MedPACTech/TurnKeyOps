export type CustomerRecord = {
 id: string; firstName: string; lastName: string; companyName?: string | null; email?: string | null;
 phone?: string | null; address?: string | null; city?: string | null; state?: string | null;
 zip?: string | null; notes?: string | null; dateCreated?: string | null; dateUpdated?: string | null;
};
export const customerName = (customer: CustomerRecord) => [customer.firstName, customer.lastName].filter(Boolean).join(' ') || customer.companyName || 'Unnamed customer';
export const triageStatuses = (current: string) => {
 const next: Record<string, string[]> = {
  new: ['in-review', 'qualified', 'contacted', 'closed'], 'in-review': ['qualified', 'contacted', 'closed'],
  'needs-info': ['in-review', 'qualified', 'contacted', 'closed'], qualified: ['contacted', 'closed'],
  contacted: ['closed'], 'inspection-scheduled': ['closed'], 'estimate-drafted': ['closed'],
  'estimate-sent': ['closed'], won: [], closed: ['in-review']
 };
 return [current, ...(next[current] ?? [])];
};
export const customerInput = (form: FormData) => {
 const values = Object.fromEntries(['firstName', 'lastName', 'companyName', 'email', 'phone', 'address', 'city', 'state', 'zip', 'notes'].map(key => [key, String(form.get(key) ?? '').trim()]));
 if (!values.firstName && !values.lastName && !values.companyName) throw new Error('Enter a contact name or company name.');
 if (values.email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(values.email)) throw new Error('Enter a valid email address.');
 if (Object.entries(values).some(([key, value]) => value.length > (key === 'notes' ? 4000 : 300))) throw new Error('One or more fields exceed the maximum length.');
 return values as Omit<CustomerRecord, 'id'>;
};
