# Domain cutover preparation — 2026-09-18

## Verified infrastructure

- Azure production web: `turnkeyops-web.azurewebsites.net`
- Resource group: `rg-turnkeyops-prod`
- Azure inbound IP (CLI `get-external-ip`): `20.115.232.15`
- Azure domain verification ID: `823EA022A034EA2F122971923593B7F6DD73FB4C8EC5A2DDF45DC9EF27E1DCD6`
- Hosting.com mail server IP: `106.0.62.93`
- Azure currently has only its default hostname bound.
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
4. Add DNS verification TXT records; bind custom hosts in Azure.
5. Switch web DNS, issue/bind certificates, verify HTTPS on every canonical and
   redirect hostname, paths/query preservation, login and public intake.

Local preparation passed domain/session tests (9), Svelte check (zero warnings
or errors), and production client build. No Azure deployment or web DNS cutover
has been performed by this preparation.
