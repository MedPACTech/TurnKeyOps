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

An Azure DevOps administrator must also disable or delete every existing
TurnKeyOps pipeline definition in the Azure DevOps UI. Repository-side removal
makes a stale definition fail to load, but disabling it removes scheduling and
manual-run ambiguity. Record that audit in the TKO-0014 release evidence.
