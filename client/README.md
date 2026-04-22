# TurnKeyOps Client

SvelteKit + Tailwind foundation for a future multi-surface TurnKeyOps app, with the BDR Admin shell implemented first.

## Current surfaces

- `BDR Public`: active tenant-facing public route with project photos and estimate CTA
- `BDR Admin`: active role-aware admin shell
- `TurnKeyOps Public`: reserved but presentable platform narrative route
- `TurnKeyOps Admin`: reserved but presentable platform-ops route

## BDR Admin roles

- `owner`
- `office-admin`
- `estimator-crew-lite`

Preview a role-specific shell state with the `role` query parameter:

`/bdr/admin/dashboard?role=office-admin`

## Development

```bash
npm run dev
```

```bash
npm run check
```
