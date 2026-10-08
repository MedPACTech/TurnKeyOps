<script lang="ts">
 import { enhance } from '$app/forms';
 import type { PageData, ActionData } from '../../../routes/carlzipf/public/$types';
 let { data, form }: { data: PageData; form: ActionData } = $props();
 let sending = $state(false);
</script>
{#if data.submitted}
 <div class="quote-form" role="status"><h3>Request received</h3><p>Your reference is {data.reference}. We’ll contact you about next steps. An appointment has not been booked.</p><a href={`${data.publicBase || '/carlzipf/public'}#quote`}>Send another request</a></div>
{:else}
<form class="quote-form" method="POST" action="?/quote" enctype="multipart/form-data" use:enhance={() => { sending = true; return async ({ update }) => { try { await update(); } finally { sending = false; } }; }}>
 <input type="hidden" name="submissionId" value={form?.submissionId ?? data.submissionId} />
 <input type="hidden" name="jobType" value="commercial" /><input type="hidden" name="requestMode" value="callback" />
 <div class="trap" aria-hidden="true"><label>Website<input name="website" tabindex="-1" autocomplete="off" /></label></div>
 {#if form?.error}<p role="alert">{form.error} Please reselect attachments before retrying.</p>{/if}
 <div class="row"><label>Full name *<input name="name" autocomplete="name" required maxlength="200" value={form?.values?.name ?? ''} /></label><label>Company<input name="company" autocomplete="organization" maxlength="200" value={form?.values?.company ?? ''} /></label></div>
 <div class="row"><label>Email *<input type="email" name="email" autocomplete="email" required value={form?.values?.email ?? ''} /></label><label>Phone *<input type="tel" name="phone" autocomplete="tel" required value={form?.values?.phone ?? ''} /></label></div>
 <label>Project location *<input name="address" autocomplete="street-address" maxlength="500" required value={form?.values?.address ?? ''} /></label>
 <label>Project type *<select name="service" required value={form?.values?.service ?? ''}><option value="" disabled>Select a service</option><option value="replacement">Complete commercial installation</option><option value="electronic">Electronic locks and access</option><option value="hardware">Architectural door hardware</option><option value="sourcing">Specialty hardware sourcing</option><option value="repair">Facility hardware upgrades</option></select></label>
 <label>Desired timeline<input name="timeline" maxlength="300" value={form?.values?.timeline ?? ''} /></label>
 <label>Project details<textarea name="details" rows="4" maxlength="3500" value={form?.values?.details ?? ''}></textarea></label>
 <label>Photos of openings or drawings<input type="file" name="photos" accept="image/jpeg,image/png,image/webp" multiple /><small>Up to 10 JPG, PNG or WebP images, 10 MB each and 45 MB total. Avoid keys or key codes. Call the shop to arrange PDF plan delivery.</small></label>
 <p>By sending this request, you agree that Carl Zipf Lock Shop may contact you about this project.</p>
 <button disabled={sending}>{sending ? 'Sending…' : 'Send project inquiry →'}</button>
</form>
{/if}
<style>
 .quote-form{background:white;color:#142129;padding:28px;border-radius:5px;display:grid;gap:14px}.row{display:grid;grid-template-columns:1fr 1fr;gap:12px}label{display:grid;gap:5px;font-size:13px;font-weight:700}input,textarea,select{box-sizing:border-box;background:white;border:1px solid #bbc4c9;border-radius:3px;width:100%;padding:11px;color:#142129;font:inherit}input:focus-visible,textarea:focus-visible,select:focus-visible,button:focus-visible{outline:3px solid #b83e17;outline-offset:3px}button{background:#b83e17;color:white;border:0;padding:14px;min-height:48px;cursor:pointer;font:inherit}button:disabled{opacity:.6}small,p{font-size:12px;font-weight:400}.trap{position:absolute;left:-10000px}@media(max-width:620px){.row{grid-template-columns:1fr}}
</style>
