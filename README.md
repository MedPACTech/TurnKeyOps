# TurnKeyOps

Unified TurnKeyOps monorepo for the API and admin applications.

## Structure

```
TurnKeyOps/
├── api/    # ASP.NET Core backend
└── admin/  # SvelteKit frontend
```

## Quick Start

### API
```bash
cd api
dotnet run
# → http://localhost:5178 (Swagger at /swagger)
```

### Admin
```bash
cd admin
npm install
npm run dev
# → http://localhost:5173
```

### Local Storage (Azurite)
```bash
npm install -g azurite
azurite --silent --location .azurite --debug .azurite/debug.log
```

## Notes

- `api/` was consolidated from the previous `turnkeyops-api` repository.
- `admin/` was consolidated from the previous `turnkeyops-client` repository and renamed from `frontend/`.
- I did not find separate local `platform` or `client` repositories in the workspace during consolidation.
