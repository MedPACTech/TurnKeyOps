# TurnKeyOps validation and deployment pipelines

The release workflow uses `main` for integration/staging and annotated
`release-*` tags for production. Pull requests are validated by one required
pipeline so no deploy pipeline can become a weaker alternate merge signal.

| Pipeline | Trigger | Purpose |
| --- | --- | --- | --- |
| `pr-validation.yml` | PRs and pushes to `main` | Required API, client, legacy admin, dependency, security, emulator E2E, mobile, and accessibility gate |
| `staging-api.yml` | API changes on `main` | Build and deploy the API to the protected Staging environment |
| `staging-web.yml` | client changes on `main` | Build and deploy the Node app to Staging, then run full smoke checks |
| `production.yml` | `release-*` tags | Revalidate, wait for UAT approval, deploy API, and publish smoke evidence |
| `turnkeyops-web.yml` | `release-*` tags | Revalidate, wait for UAT approval, deploy web, and publish smoke evidence |
| `bdr-admin.yml` | `release-*` tags | Revalidate, wait for approval, and deploy the retained legacy static admin |

## Azure DevOps setup

Create the six Azure DevOps pipelines above from their YAML files. Add the
`Required PR validation` stage as a required `main` branch policy and protect
the `Production` and `Staging` environments.

Define these pipeline variables (or place them in a linked variable group):

| Variable | Secret | Used by |
| --- | --- | --- |
| `AZURE_SERVICE_CONNECTION` | No | Name of the Azure Resource Manager service connection for the client App Service |
| `TURNKEYOPS_PRODUCTION_RESOURCE_GROUP` | No | Production resource group |
| `TURNKEYOPS_STAGING_RESOURCE_GROUP` | No | Staging resource group |
| `TURNKEYOPS_API_WEB_APP_NAME` | No | Production API App Service |
| `TURNKEYOPS_WEB_APP_NAME` | No | Production Node App Service |
| `TURNKEYOPS_STAGING_API_WEB_APP_NAME` | No | Staging API App Service |
| `TURNKEYOPS_STAGING_WEB_APP_NAME` | No | Staging Node App Service |
| `TURNKEYOPS_API_BASE_URL` | No | Production API origin |
| `TURNKEYOPS_WEB_BASE_URL` | No | Production web origin |
| `TURNKEYOPS_STAGING_API_BASE_URL` | No | Staging API origin |
| `TURNKEYOPS_STAGING_WEB_BASE_URL` | No | Staging web origin |
| `RELEASE_APPROVERS` | No | Newline-separated Azure DevOps users/groups notified by ManualValidation |
| `BDR_ADMIN_STATIC_WEB_APP_TOKEN` | Yes | Deployment token from the existing BDR Admin Static Web App |

The service connection must be authorized for the client pipeline and have
permission to update and deploy the target App Service. Mark the Static Web
Apps deployment token as secret.

Production pipelines cannot deploy from a branch. They require a `release-*`
tag, successful validation, a ManualValidation UAT decision, and the Azure
DevOps `Production` environment approval. The complete operational contract,
UAT template, evidence inventory, and rollback procedure are in
`docs/release-readiness.md`.

## Azure resource expectations

The BDR client App Service must be Linux-based and capable of running Node 22.
The pipeline sets its startup command to `node build`.

The BDR Admin target must be an Azure Static Web App. The repository's
`admin/staticwebapp.config.json` is copied into the build output by SvelteKit
and deployed with the site.
