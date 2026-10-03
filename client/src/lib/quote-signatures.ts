export type QuoteApprovalSignatureInput = {
 signerPrintedName: string;
 intentToSign: true;
 consentVersion: string;
 revisionNumber: number;
 documentHash: string;
};
export type QuoteApprovalSignature = {
 signerPrintedName: string; consentText: string; consentVersion: string; signedAtUtc: string;
 revisionNumber: number; total: number; documentHash: string; method: 'typed-name'; quoteRequestId: string;
};

/** Validate customer intent before calling the authoritative signing endpoint. */
export function parseQuoteSignatureInput(data: FormData): { signature?: QuoteApprovalSignatureInput; error?: string; signerPrintedName: string } {
 const signerPrintedName = String(data.get('signerPrintedName') ?? '').trim();
 const invalid = (error: string) => ({ error, signerPrintedName });
 if (data.get('intentToSign') !== 'yes') return invalid('Select the checkbox to confirm your intent to electronically sign and approve this quote.');
 if (!signerPrintedName || signerPrintedName.length > 200 || /[\u0000-\u001f\u007f]/.test(signerPrintedName)) return invalid('Enter your printed name using 1 to 200 characters.');
 const revision = String(data.get('revisionNumber') ?? '');
 const revisionNumber = Number(revision);
 const consentVersion = String(data.get('consentVersion') ?? '');
 const documentHash = String(data.get('documentHash') ?? '');
 if (!/^[1-9]\d*$/.test(revision) || !Number.isSafeInteger(revisionNumber) || consentVersion !== 'quote-approval-v1' || !/^[a-f0-9]{64}$/i.test(documentHash)) return invalid('This quote’s signing details are unavailable or outdated. Reload the current quote before signing.');
 return { signerPrintedName, signature: { signerPrintedName, intentToSign: true, consentVersion, revisionNumber, documentHash } };
}
