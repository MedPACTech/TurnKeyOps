# TurnKeyOps

Unified TurnKeyOps monorepo for the API, admin, and client applications.

## Structure

```
TurnKeyOps/
├── api/    # ASP.NET Core backend
├── admin/  # Internal admin app
└── client/ # Public-facing client site
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

### Client
```bash
cd client
npm install
npm run dev
```

### Local Storage (Azurite)
```bash
npm install -g azurite
azurite --silent --location .azurite --debug .azurite/debug.log
```

## Notes

- `api/` was consolidated from the previous `turnkeyops-api` repository.
- `admin/` was consolidated from the previous `turnkeyops-client` repository and renamed from `frontend/`.
- `client/` was added from the older `turnkeyops-client` app that contains the public-facing site foundation.
- I did not find a separate local `platform` repository in the workspace during consolidation.
