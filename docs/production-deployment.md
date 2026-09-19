# TurnKeyOps production deployment

TurnKeyOps uses one multi-tenant SvelteKit Node application and one ASP.NET
Core API. The web application resolves the tenant and surface from the request
hostname; do not create separate BDR and Think Pink builds.

## Production surfaces

| Hostname | Application route | Purpose |
| --- | --- | --- |
| `turnkeyops.ai` | `/turnkeyops/public` | TurnKeyOps public site |
| `www.turnkeyops.ai` | `/turnkeyops/public` | Public-site alias |
| `admin.turnkeyops.ai` | `/turnkeyops/admin` | Platform administration |
| `thinkpinklandclearing.com` | `/thinkpink/public` | Think Pink public site |
| `www.thinkpinklandclearing.com` | `/thinkpink/public` | Public-site alias |
| `admin.thinkpinklc.com` | `/thinkpink/admin` | Think Pink External Admin |
| `bdrconcrete.com` | `/bdr/public` | BDR public site |
| `www.bdrconcrete.com` | `/bdr/public` | Public-site alias |
| `admin.bdrconcrete.com` | `/bdr/admin` | BDR External Admin |

Hostname routing is defined in `client/src/lib/config/domains.ts`. External
Admin tenant configuration is defined in
`client/src/lib/config/external-admin.ts`.

## Azure resources

Create these production resources:

1. A Linux App Service Plan using a production tier that supports custom domains and managed certificates.
2. A Node 22 Linux Web App for the SvelteKit application.
3. A .NET 10 Linux Web App for the TurnKeyOps API.
4. A production Azure Storage account for tables, blobs, and queues.
5. Azure Communication Services resources for OTP email and SMS.
6. Application Insights for both web applications.
7. Azure Key Vault for production secrets.

The API and Node application must not use Azurite or `.svelte-kit` local JSON
stores in production.

## Deployment configuration

GitHub Actions and Hubbsly Ship are authoritative. Azure DevOps deployment
definitions are retired and must remain disabled. Configure the `staging` and
`production` GitHub environments with the OIDC secrets and target variables
listed in `docs/release-readiness.md`.

Azure remains the application runtime; it is no longer the release control
plane. GitHub Actions builds and deploys, and Hubbsly Ship initiates and tracks
production releases. No Azure DevOps pipeline, service connection, variable
group, release tag, or manual-validation group is part of this contract.

The Azure federated identity subject must be restricted to the matching GitHub
environment in `MedPACTech/TurnKeyOps`. API secrets belong in Azure App Service
settings or Key Vault references and must never be copied into a GitHub
workflow, repository variable, client bundle, or deployment artifact. The
identity, communications, billing, rotation, disable, rollback, and smoke
contract is in `api/docs/production-integrations.md`.

## Deployment workflows

Production has three input-free GitHub Actions entry points for Hubbsly:

| Hubbsly workflow | File | Packages and deploys |
| --- | --- | --- |
| Deploy TurnKeyOps - Production API | `deploy-production-api.yml` | API only |
| Deploy TurnKeyOps - Production Web | `deploy-production-web.yml` | Shared web app, covering all six surfaces |
| Deploy TurnKeyOps - Production (API + Web) | `deploy-production.yml` | Both, with API readiness checked before web deployment |

Select `main` and initiate the desired run. The run itself is the human
production approval. No release ID, UAT link, rollback reference, or second
GitHub environment approval is required. PR validation and branch protections
remain. Refresh Hubbsly's workflow definitions after merge to discover the new
entry points.

Use Web for changes confined to `client/`; use API for changes confined to
`api/`. Use Both for coordinated contract changes or when dependencies span
both components. Independent deployment assumes the new component remains
compatible with the currently deployed counterpart. Component selection is
explicit; there is no last-commit-only change detection that could overlook
changes since an unsuccessful deployment.

All runs retain the full quality workflow, including cross-component browser
tests. Only the selected component is packaged and deployed; Web does not
change API configuration or restart the API, and API does not deploy or
restart the web app. Deployment records include component, SHA, run ID,
attempt, artifact hashes, and smoke results. Existing rollback scripts accept
the selected component's ZIP as before.

Staging remains an automatic combined deployment on `main`. Production
entry points share a production concurrency group; staging and production
also share a deployment-job lock because their apps share one hosting plan.
GitHub's concurrency queue retains at most one pending job per group; a newer
pending request can replace an older pending request. Running deployments are
not cancelled. Builds run on GitHub runners and may execute concurrently.

Smoke checks have a 300-second elapsed-time budget per invocation, including
HTTP timeouts and retry delays. Override with `SMOKE_TIMEOUT_SECONDS` when
necessary. API-only smoke probes API health and anonymous-access rejection;
Web/Both smoke also probes public pages and all three admin redirects. Web
smoke verifies the existing API dependency without deploying it. Both runs
include a separate bounded API-readiness invocation before deploying web.

The runtime remains one .NET API and one Node web app per environment. These
workflows do not split the six surfaces into separate runtime applications,
change hosting tiers, or provision additional resources.

## First activation checklist

1. Confirm the legacy Azure DevOps definitions are inactive; if none remain,
   record that result as `not applicable` rather than provisioning Azure
   DevOps solely for this audit.
2. Create the `staging` and `production` GitHub environments and configure the
   secrets, variables, OIDC subjects, and branch restrictions in
   `docs/release-readiness.md`.
3. Add the required `main` ruleset and its `Required PR validation` check.
4. Connect the repository to Hubbsly Ship and configure the production
   workflow dispatch contract.
5. Merge through the ruleset and retain the first successful staging workflow,
   deployment evidence artifact, and smoke log.
6. Trigger production through Ship and retain the resulting GitHub run.
   Dispatch is the human approval; checks and deployment proceed automatically.

## Custom domains and TLS

Add every hostname in the production-surfaces table to the Node Web App. Azure
will provide the verification records.

For each apex domain:

1. Add Azure's `asuid` TXT verification record.
2. Replace the existing apex A record with the Web App inbound IP.

For each `www` and `admin` hostname:

1. Add the Azure `asuid.<subdomain>` TXT verification record when requested.
2. Add a CNAME to the Node Web App default `azurewebsites.net` hostname.

After Azure validates each hostname, create and bind an App Service managed
certificate. Keep the existing A2 Hosting and Namecheap records in place until
the Azure default hostname and all custom-domain validations pass.

## Release verification

Before changing DNS:

1. Confirm `Required PR validation` passed for the exact merge commit.
2. Confirm the staging deployment and its published smoke artifact passed.
3. Verify the Web App default hostname using explicit `Host` headers for all production domains.
4. Confirm each public hostname returns `200`.
5. Confirm each admin hostname redirects an anonymous browser to `/auth/login` with the correct tenant return path.
6. Submit one test request per tenant and confirm it appears only in that tenant's External Admin.
7. Complete an OTP login for each admin hostname.
8. Verify uploaded files persist after an App Service restart.
9. Retain any manual testing notes with the release; these are optional
   documentation, not required workflow inputs.

## DNS cutover

Lower DNS TTLs at least several hours before cutover. Change one public domain
at a time, verify TLS and form submission, and then add its admin subdomain. Do
not remove the previous hosting configuration until the new deployment has
remained healthy through the rollback window.

## Canonical redirects and email

Azure serves permanent HTTP 308 redirects, preserving paths and query strings:

- `thinkpinklc.com` and `www.thinkpinklc.com` → `thinkpinklandclearing.com`
- `bdr.construction` and `www.bdr.construction` → `bdrconcrete.com`
- The `www` variants of each public site → its apex domain

Bind and certificate every redirect hostname on the same Node Web App. DNS
alone does not implement these redirects. Replace registrar parking/forwarding
only after the corresponding Azure application and hostname are ready.

Hosting.com/cPanel hosts email for `bdrconcrete.com`, `thinkpinklc.com`, and
`turnkeyops.ai`. DNS may remain with Namecheap or Hosting.com. Keep MX and mail
host A records pointing to Hosting.com, with its SPF, DKIM and DMARC records.
Do not point mail hosts at Azure or use an alias to the website apex. Provision
`chance@thinkpinklc.com` before changing its MX from Namecheap forwarding.
