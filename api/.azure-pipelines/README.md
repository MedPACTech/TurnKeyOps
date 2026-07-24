# Frontend deployment pipelines

The two frontend surfaces use different Azure hosting products because they
have different runtime requirements:

| Pipeline | Source | Azure target | Reason |
| --- | --- | --- | --- |
| `bdr-client.yml` | `/client` | Linux Azure App Service (Node 22) | Uses SvelteKit server routes, cookies, and server-side operations |
| `bdr-admin.yml` | `/admin` | Azure Static Web Apps | Produces a fully static SPA in `/admin/build` |

## Azure DevOps setup

Create two Azure DevOps pipelines from the existing repository:

1. Select `api/.azure-pipelines/bdr-client.yml`.
2. Select `api/.azure-pipelines/bdr-admin.yml`.

Define these pipeline variables (or place them in a linked variable group):

| Variable | Secret | Used by |
| --- | --- | --- |
| `AZURE_SERVICE_CONNECTION` | No | Name of the Azure Resource Manager service connection for the client App Service |
| `BDR_CLIENT_WEB_APP_NAME` | No | Existing Linux Azure App Service name for the Node client |
| `BDR_ADMIN_STATIC_WEB_APP_TOKEN` | Yes | Deployment token from the existing BDR Admin Static Web App |

The service connection must be authorized for the client pipeline and have
permission to update and deploy the target App Service. Mark the Static Web
Apps deployment token as secret.

Both pipelines build and validate pull requests but deploy only from `main`.
Azure DevOps `Production` environment approvals can be enabled without
changing the YAML.

## Azure resource expectations

The BDR client App Service must be Linux-based and capable of running Node 22.
The pipeline sets its startup command to `node build`.

The BDR Admin target must be an Azure Static Web App. The repository's
`admin/staticwebapp.config.json` is copied into the build output by SvelteKit
and deployed with the site.
