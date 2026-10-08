export type CustomerRecord = {
 customerType?: 'residential' | 'commercial' | null;
 id: string; firstName: string; lastName: string; companyName?: string | null; email?: string | null;
 phone?: string | null; address?: string | null; city?: string | null; state?: string | null;
 zip?: string | null; notes?: string | null; dateCreated?: string | null; dateUpdated?: string | null;
};
export const customerName = (customer: CustomerRecord) => (customer.customerType === 'commercial' ? customer.companyName : '') || [customer.firstName, customer.lastName].filter(Boolean).join(' ') || customer.companyName || 'Unnamed customer';
export const customerInput = (form: FormData) => {
 const customerType = String(form.get('customerType') ?? '').trim();
 if (customerType && !['residential','commercial'].includes(customerType)) throw new Error('Choose Residential or Commercial.');
 const values = Object.fromEntries(['firstName', 'lastName', 'companyName', 'email', 'phone', 'address', 'city', 'state', 'zip', 'notes'].map(key => [key, String(form.get(key) ?? '').trim()]));
 if (!values.firstName && !values.lastName && !values.companyName) throw new Error('Enter a contact name or company name.');
 if (values.email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(values.email)) throw new Error('Enter a valid email address.');
 if (Object.entries(values).some(([key, value]) => value.length > (key === 'notes' ? 4000 : 300))) throw new Error('One or more fields exceed the maximum length.');
 if (customerType === 'commercial' && !values.companyName) throw new Error('Enter the company name.');
 if (customerType === 'residential' && !values.firstName && !values.lastName) throw new Error('Enter the customer name.');
 return {...values, ...(customerType ? {customerType} : {})} as Omit<CustomerRecord, 'id'>;
};
