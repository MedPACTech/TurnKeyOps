import assert from 'node:assert/strict';
import test from 'node:test';
import { customerInput, customerName, triageStatuses } from '../src/routes/carlzipf/admin/records.ts';
test('triage does not claim a booking, quote, or signed sale', () => {
 for (const status of ['new', 'in-review', 'contacted', 'closed']) {
  const options = triageStatuses(status);
  assert.ok(!options.includes('inspection-scheduled'));
  assert.ok(!options.includes('estimate-sent'));
  assert.ok(!options.includes('won'));
 }
 assert.deepEqual(triageStatuses('won'), ['won']);
 assert.deepEqual(triageStatuses('closed'), ['closed', 'in-review']);
});
test('customer input requires identity, validates email, and ignores submitted IDs', () => {
 const form = new FormData();
 assert.throws(() => customerInput(form), /name/);
 form.set('companyName', '  Acme Properties ');
 form.set('id', 'not-authorized');
 form.set('email', 'bad');
 assert.throws(() => customerInput(form), /email/);
 form.set('email', 'contact@example.com');
 const parsed = customerInput(form);
 assert.equal(parsed.companyName, 'Acme Properties');
 assert.ok(!('id' in parsed));
 assert.equal(customerName({ ...parsed, id: '1' }), 'Acme Properties');
 form.set('notes', 'x'.repeat(4001));
 assert.throws(() => customerInput(form), /maximum length/);
});
