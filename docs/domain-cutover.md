# Domain cutover preparation — 2026-09-18

## Verified infrastructure

- Azure production web: `turnkeyops-web.azurewebsites.net`
- Resource group: `rg-turnkeyops-prod`
- Azure inbound IP (CLI `get-external-ip`): `20.115.232.15`
- Azure domain verification ID: `823EA022A034EA2F122971923593B7F6DD73FB4C8EC5A2DDF45DC9EF27E1DCD6`
- Hosting.com mail server IP: `106.0.62.93`
- Azure now has all 13 requested custom hostnames bound (including www aliases).
  Custom-domain TLS certificates and web DNS cutover are still pending.
- All three production public paths returned 200; all three admin paths
  redirected anonymous HTML requests to the corresponding login return path.
  This is availability checking, not completed login/form UAT.

## Web DNS records to apply after application deployment

All entries route to the same Azure Node web application. Each needs a matching
Azure hostname binding and TLS certificate. Keep existing web records until
Azure ownership verification and the updated deployment are ready; replace
conflicting parking/URL-forwarding records during cutover.

| DNS zone | Provider | A record host → value | CNAME hosts → value |
| --- | --- | --- | --- |
| turnkeyops.ai | Hosting.com | @ → 20.115.232.15 | www, admin → turnkeyops-web.azurewebsites.net |
| bdrconcrete.com | Hosting.com | @ → 20.115.232.15 | www, admin → turnkeyops-web.azurewebsites.net |
| bdr.construction | Hosting.com | @ → 20.115.232.15 | www → turnkeyops-web.azurewebsites.net |
| thinkpinklc.com | Namecheap | @ → 20.115.232.15 | www, admin → turnkeyops-web.azurewebsites.net |
| thinkpinklandclearing.com | Namecheap | @ → 20.115.232.15 | www → turnkeyops-web.azurewebsites.net |

For every zone above, add TXT hosts `asuid` and `asuid.www` with the Azure
verification ID. Also add `asuid.admin` on the three zones with an admin host.
Use a 300-second TTL where supported during cutover; existing Hosting.com
records have 14400-second TTLs, so lower those sufficiently ahead of switching.
DNS provider and registrar do not need to change.

Redirects handled by the application on Azure:

- thinkpinklc.com → https://thinkpinklandclearing.com
- bdr.construction → https://bdrconcrete.com
- www versions → the corresponding canonical apex, directly

## Hosting.com email

Existing BDR Concrete and TurnKeyOps MX, mail A, DKIM and DMARC records remain
on Hosting.com. Their mail hosts already have direct A records, independent of
the web apex. Review SPF `a` mechanisms during cutover: authorizing the website
IP is unnecessary when Azure does not send mailbox email. Calendar/contact
SRV records currently target the web apex; if used, retarget them to a validated
Hosting.com service hostname before moving the apex.

Created `thinkpinklc.com` in the existing Hosting.com cPanel account with its
own document root. This provisions the domain for email; authoritative DNS
remains Namecheap. Public web DNS is unchanged. Think Pink mail DNS has been switched to Hosting.com.

Mailbox `chance@thinkpinklc.com` was created by the user and verified as
Unrestricted in cPanel. Email Routing is set to Local Mail Exchanger.
`robb@bdrconcrete.com` was also created by the user and verified as Unrestricted
with a 250 MB quota. Actual send/receive tests remain pending.

Namecheap Email Forwarding was replaced with Custom MX. The following records
were published and verified directly against dns1.registrar-servers.com:

| Type | Host | Value | Priority |
| --- | --- | --- | --- |
| A | mail | 106.0.62.93 | |
| MX | @ | mail.thinkpinklc.com | 0 |
| TXT | @ | v=spf1 mx ip4:106.0.62.93 include:spf.a2hosting.com ~all | |
| TXT | _dmarc | v=DMARC1; p=none; | |
| TXT | default._domainkey | Hosting.com public DKIM key published and verified | |

Publish exactly one SPF record; replace the Namecheap forwarding SPF. Preserve
Hosting.com email service records such as mail/webmail/autodiscover as needed.
Check TLS for the chosen mail client hostname; do not route it to Azure.

## Remaining gates

1. Mailbox creation, local routing and authoritative DNS verification are complete.
2. Test actual receipt and sending; verify mail-client TLS configuration.
3. Review and deploy application routing/CORS changes through existing GitHub
   staging, production approval and Hubbsly Ship release process.
4. Completed: all 13 ownership TXT records published; all 13 custom hosts bound in Azure.
5. Switch web DNS, issue/bind certificates, verify HTTPS on every canonical and
   redirect hostname, paths/query preservation, login and public intake.

Local preparation passed domain/session tests (9), Svelte check (zero warnings
or errors), and production client build. Production deployment has since completed; see the current status below.

## Active continuation

- Routing PR: https://github.com/MedPACTech/TurnKeyOps/pull/17
- Branch: `codex/domain-cutover`; commit `16a9412`
- Isolated worktree: `/tmp/turnkeyops-domain-cutover` (based on main `96bafe8`)
- Required independent PR review and GitHub CI precede merge. Production then
  requires protected approval and real Hubbsly Ship/UAT evidence; no Hubbsly
  MCP tools were available during preparation. Never invent release evidence.
- A 15-minute task heartbeat `continue-turnkeyops-domain-cutover` is active.
- Azure verification and hostname bindings are complete. PR #17 passed every CI
  check and was approved and merged as 2b536c18bff6a37008ec0dc5f08bf08493ea3790.
  Staging run 35374559087 succeeded; all six staging surfaces verified.
  Public web A/CNAME cutover waits until updated production deployment and TLS.

### 2026-09-18 heartbeat progress

Published asuid/asuid.www in all five DNS zones and asuid.admin in the three
admin zones. Azure accepted all 13 custom hostnames; verified the final list
with Azure CLI. Existing web A/CNAME and email records were not changed.
Think Pink public bindings initially waited for DNS propagation, then succeeded.

### Merge and staging

PR #17 merged at commit `2b536c18bff6a37008ec0dc5f08bf08493ea3790`.
Staging: https://github.com/MedPACTech/TurnKeyOps/actions/runs/35374559087
Previous successful production/rollback run: 34083762336.
Requested real Hubbsly Ship release ID and approved UAT evidence from the user
for the production workflow; those inputs are still missing.

### 2026-09-18 17:57 UTC continuation

Staging run 35374559087 completed successfully, including API, client, E2E,
artifact and deploy jobs. Direct staging smoke returned 200 on all three public
paths and 303 login redirects with correct return paths for all three admins.
No new production workflow has started. Production remains blocked on real
Hubbsly Ship release ID and approved UAT evidence, then protected approval.
Web DNS and certificate bindings remain unchanged.

### Production authorization

The user explicitly authorized production release in this task: “If you need
to release to production, go ahead.” Do not request that authorization again.
Current main remains 2b536c18bff6a37008ec0dc5f08bf08493ea3790.
The live GitHub production environment requires mp-christian-armstrong review
with prevent_self_review enabled. The workflow requires a real Hubbsly Ship
release ID and approved UAT evidence. No Hubbsly MCP or tool-search capability
is currently available. These missing release inputs, not user permission,
prevent dispatch. Do not fabricate a Ship identifier or repurpose old evidence.

### Current status — 2026-09-18 19:15 UTC

Supersedes earlier pending-production notes: production run 35379752203
succeeded for merge commit 2b536c18bff6a37008ec0dc5f08bf08493ea3790.
GitHub approval is cleared.

Removed stale fixed ORIGIN from production and staging web app settings so
request hostnames can select the correct surface. HOST_HEADER is restored to
x-forwarded-host on both apps, matching deployment workflows; PROTOCOL_HEADER
remains x-forwarded-proto. Earlier custom-Host probes through the environment
proxy misleadingly returned the default surface. Direct curl --noproxy '*'
confirms canonical public routing and all seven 308 redirects. BDR public had
a timeout and is being retried; admin requests need Accept: text/html to
exercise login redirects (otherwise 401 is expected).

Azure managed certificate creation requires live A/CNAME traffic DNS, even
with completed asuid validation. Added the previously absent CNAME
admin.turnkeyops.ai -> turnkeyops-web.azurewebsites.net through cPanel and
verified against ns1.a2hosting.com. Certificate issuance is in progress.
All apex/www web DNS and all mail records remain unchanged.

### 2026-09-18 19:20 UTC continuation

All three canonical public hosts returned 200 on direct Azure Host-header probes.
All three admin hosts with Accept: text/html returned 303 to the correct
/auth/login?returnTo path. Seven redirect aliases returned correct 308s.

Published both admin.turnkeyops.ai and admin.bdrconcrete.com CNAMEs pointing
to turnkeyops-web.azurewebsites.net, verified authoritative DNS. Both Azure
managed certificate create commands finished with creation-in-progress notices:
- az webapp config ssl show -g rg-turnkeyops-prod --certificate-name admin.turnkeyops.ai
- az webapp config ssl show -g rg-turnkeyops-prod --certificate-name admin.bdrconcrete.com
At last check, TurnKeyOps certificate show returned Not Found and ssl list
was empty, so no thumbprints are available and neither certificate is bound.
Do not treat CLI exit 0 / progress warning as certificate completion.

Set apex and www TTL fields to 300 for turnkeyops.ai, bdrconcrete.com, and
bdr.construction through cPanel. Authoritative answers show apex TTL 30 and
www TTL 300; apex still 106.0.62.93 and www still points at each apex. MX
records remain mail.DOMAIN at priority 0. No mailbox DNS was modified.

Namecheap session expired while opening thinkpinklc.com Advanced DNS. Asked
the user asynchronously to sign in again; Chrome tab 2019175039 is left on
Namecheap login with its return URL set to thinkpinklc.com Advanced DNS.
Think Pink admin CNAME has NOT been added; no Think Pink web records changed.
Next: inspect certificate issuance, bind SNI certificates and verify real
admin HTTPS; then continue public DNS/certificates. If issuance does not
appear, inspect Azure resource operation status before reissuing requests.

### 2026-09-18 19:40 UTC cutover

User restored Namecheap login. All 13 desired web DNS records are now published
and verified at authoritative DNS: five apex A records 20.115.232.15, five www
CNAMEs and three admin CNAMEs turnkeyops-web.azurewebsites.net. All MX and mail
A/SPF/DKIM/DMARC records are unchanged. Namecheap thinkpinklc.com had no web
apex/www records in its full list, so new ones were added. Thinkpinklandclearing
apex URL redirect was converted to an A record; www parking CNAME replaced.

All three admin certificates are issued and SNI-bound. TurnKeyOps and BDR
admin HTTPS validated with 303 to correct login; Think Pink final HTTPS check
pending. Public certificates bound so far: www.bdrconcrete.com,
thinkpinklandclearing.com, www.thinkpinklandclearing.com. Other seven certificate
bindings are being completed by bounded CLI process session 77520.

Azure ssl list misleadingly returns []; query ssl show by exact hostname
instead. Create commands often return progress warnings and an SDK
deserialization warning even when Azure later succeeds. Check exact resource
and activity log; do not reissue while pending. Turnkeyops.ai apex initially
failed cached-DNS validation, then retried after authoritative verification
(session 70449). Thinkpinklc.com apex/www issuance session 92747 still running.
Do not call cutover complete until all 13 real HTTPS checks pass.

### 2026-09-18 19:42 UTC verification

Twelve of thirteen custom hostnames now have SNI certificates bound and pass
real HTTPS validation. Both BDR and Think Pink canonical public hosts return
200; all three admins return 303 to the proper login return path. All seven
redirect aliases return 308 preserving /cutover-check?source=dns&check=1.
The www.turnkeyops.ai redirect itself is valid, but its destination apex
still awaits its certificate.

The final turnkeyops.ai certificate retry is now accepted and being issued
(session 2307); earlier attempts failed because Azure cached the former IP.
All four authoritative Hosting.com nameservers and Google/Cloudflare DNS
now return 20.115.232.15. Bounded bind process 77520 waits on this last cert.
Mail host A records remain 106.0.62.93 for all three mail domains, with their
MX and SPF records preserved. Actual mailbox send/receive and authenticated
admin/intake UAT were not performed during DNS verification.

### COMPLETE — web DNS and HTTPS, 2026-09-18 19:45 UTC

All 13 hostnames are published, Azure-bound, certificate-bound (SNI), and
verified over real HTTPS with normal certificate validation. Final
turnkeyops.ai certificate thumbprint: 440AC77453E90690A7EE99456EDCA58D4FCA61FB.
Its public root returned HTTP 200 at 19:45:25 UTC.

Verified outcomes:
- turnkeyops.ai, bdrconcrete.com, thinkpinklandclearing.com: public root 200.
- admin.turnkeyops.ai, admin.bdrconcrete.com, admin.thinkpinklc.com: 303 to
  /auth/login with the correct tenant/admin return path for anonymous HTML.
- thinkpinklc.com -> thinkpinklandclearing.com and bdr.construction ->
  bdrconcrete.com: 308, path and query preserved.
- All five www aliases: 308 directly to their canonical public apex, preserving
  path and query.

Hosting.com email DNS was preserved: MX mail.DOMAIN priority 0 and mail A
106.0.62.93 for bdrconcrete.com, turnkeyops.ai, and thinkpinklc.com. Existing
SPF/DKIM/DMARC records unchanged by the web cutover. Think Pink mailbox DNS
was configured earlier as documented above.

Scope of verification: DNS, valid HTTPS, public availability, anonymous admin
login routing, redirects. Actual authenticated admin workflows, public intake
submission, and mailbox send/receive tests remain user acceptance checks.
The recurring cutover follow-up can now be paused.
