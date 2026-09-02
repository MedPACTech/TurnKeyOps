# Azure DevOps deployment retirement

TurnKeyOps deployment is owned by GitHub Actions and Hubbsly Ship. The Azure
DevOps YAML definitions previously stored in this directory were removed so an
existing Azure DevOps pipeline cannot deploy a new repository commit from a
branch or tag trigger.

The retired definitions remain available in Git history through commit
`9244c1fafe7f407df5e719187e0ae9d0f1243165`. Do not restore them as active
pipelines. The authoritative workflows are:

- `.github/workflows/pull-request.yml`
- `.github/workflows/quality-gates.yml`
- `.github/workflows/deploy.yml`
- `.github/workflows/deploy-staging.yml`
- `.github/workflows/deploy-production.yml`

If TurnKeyOps pipeline definitions still exist in an Azure DevOps organization,
an administrator should disable or delete them to remove scheduling and
manual-run ambiguity. Repository-side removal already prevents those YAML
definitions from loading a new commit. Record the retirement check in the
TKO-0014 evidence; record `not applicable` when no active definitions or Azure
DevOps project remain. Do not recreate an Azure DevOps project, service
connection, variable group, environment, or approver group for this check.
