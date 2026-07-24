# TurnKeyOps production deployment

TurnKeyOps uses one multi-tenant SvelteKit Node application and one ASP.NET Core API. The web application resolves the tenant and surface from the request hostname; do not create separate BDR and Think Pink builds.

## Production surfaces

| Hostname | Application route | Purpose |
| --- | --- | --- |
| `turnkeyops.ai` | `/turnkeyops/public` | TurnKeyOps public site |
| `www.turnkeyops.ai` | `/turnkeyops/public` | Public-site alias |
| `admin.turnkeyops.ai` | `/turnkeyops/admin` | Platform administration |
| `thinkpinklc.com` | `/thinkpink/public` | Think Pink public site |
| `www.thinkpinklc.com` | `/thinkpink/public` | Public-site alias |
| `admin.thinkpinklc.com` | `/thinkpink/admin` | Think Pink External Admin |
| `bdrconcrete.com` | `/bdr/public` | BDR public site |
| `www.bdrconcrete.com` | `/bdr/public` | Public-site alias |
| `admin.bdrconcrete.com` | `/bdr/admin` | BDR External Admin |

Hostname routing is defined in `client/src/lib/config/domains.ts`. External Admin tenant configuration is defined in `client/src/lib/config/external-admin.ts`.

## Azure resources

Create these production resources:

1. A Linux App Service Plan using a production tier that supports custom domains and managed certificates.
2. A Node 22 Linux Web App for the SvelteKit application.
3. A .NET 10 Linux Web App for the TurnKeyOps API.
4. A production Azure Storage account for tables, blobs, and queues.
5. Azure Communication Services resources for OTP email and SMS.
6. Application Insights for both web applications.
7. Azure Key Vault for production secrets.

The API and Node application must not use Azurite or `.svelte-kit` local JSON stores in production.

## Azure DevOps variables

Create a secured production variable group containing:

- `AZURE_SERVICE_CONNECTION`
- `TURNKEYOPS_PRODUCTION_RESOURCE_GROUP`
- `TURNKEYOPS_WEB_APP_NAME`
- `TURNKEYOPS_API_WEB_APP_NAME`
- `TURNKEYOPS_API_BASE_URL`

`TURNKEYOPS_API_BASE_URL` must be the API origin without a trailing slash, for example `https://api.turnkeyops.ai`.

Configure all API secrets through App Service settings or Key Vault references. Never copy `.local/user-secrets.json` into a deployment artifact.

## Pipelines

- Web: `api/.azure-pipelines/turnkeyops-web.yml`
- API: `api/.azure-pipelines/production.yml`

Both deploy from `main`. Configure the Azure DevOps pipeline definitions only after their service connection and secured variables exist.

## Custom domains and TLS

Add every hostname in the production-surfaces table to the Node Web App. Azure will provide the verification records.

For each apex domain:

1. Add Azure's `asuid` TXT verification record.
2. Replace the existing apex A record with the Web App inbound IP.

For each `www` and `admin` hostname:

1. Add the Azure `asuid.<subdomain>` TXT verification record when requested.
2. Add a CNAME to the Node Web App default `azurewebsites.net` hostname.

After Azure validates each hostname, create and bind an App Service managed certificate. Keep the existing A2 Hosting and Namecheap records in place until the Azure default hostname and all custom-domain validations pass.

## Release verification

Before changing DNS:

1. Run `npm ci`, `npm run check`, and `npm run build` in `client`.
2. Run `dotnet restore`, `dotnet build`, and `dotnet test` against `api/TurnKeyOps.API.sln`.
3. Verify the Web App default hostname using explicit `Host` headers for all production domains.
4. Confirm each public hostname returns `200`.
5. Confirm each admin hostname redirects an anonymous browser to `/auth/login` with the correct tenant return path.
6. Submit one test request per tenant and confirm it appears only in that tenant's External Admin.
7. Complete an OTP login for each admin hostname.
8. Verify uploaded files persist after an App Service restart.

## DNS cutover

Lower DNS TTLs at least several hours before cutover. Change one public domain at a time, verify TLS and form submission, and then add its admin subdomain. Do not remove the previous hosting configuration until the new deployment has remained healthy through the rollback window.
