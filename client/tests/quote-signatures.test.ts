import test from 'node:test';
import assert from 'node:assert/strict';
import { parseQuoteSignatureInput } from '../src/lib/quote-signatures.ts';
const form = (overrides: Record<string, string> = {}) => {
 const data = new FormData();
 for (const [key, value] of Object.entries({ signerPrintedName: '  Demo Customer  ', intentToSign: 'yes', consentVersion: 'quote-approval-v1', revisionNumber: '3', documentHash: 'ab'.repeat(32), ...overrides })) data.set(key, value);
 return data;
};
test('typed signature requires affirmative intent independently of a printed name', () => {
 for (const intentToSign of ['', 'no', 'true']) assert.ok(parseQuoteSignatureInput(form({intentToSign})).error);
});
test('printed name validation rejects blank, control characters and overlength while preserving international names', () => {
 for (const signerPrintedName of ['   ', 'a'.repeat(201), 'Demo\nCustomer']) assert.ok(parseQuoteSignatureInput(form({signerPrintedName})).error);
 assert.equal(parseQuoteSignatureInput(form({signerPrintedName: ' 王 小明 '})).signature?.signerPrintedName, '王 小明');
});
test('signature submission binds the exact revision, consent version, and document hash', () => {
 for (const overrides of [{revisionNumber:'0'}, {revisionNumber:'1.5'}, {revisionNumber:'9007199254740992'}, {documentHash:'short'}, {consentVersion:'unknown'}]) assert.ok(parseQuoteSignatureInput(form(overrides)).error);
 const result = parseQuoteSignatureInput(form());
 assert.deepEqual(result.signature, {signerPrintedName:'Demo Customer', intentToSign:true, consentVersion:'quote-approval-v1', revisionNumber:3, documentHash:'ab'.repeat(32)});
});
