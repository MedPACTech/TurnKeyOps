# Tenant sign-in and workspace selection

External sign-in URLs are `/bdr/auth/login`, `/thinkpink/auth/login`, and `/carlzipf/auth/login`. Legacy `/auth/login?returnTo=...` links redirect to the matching tenant URL. A tenant login only accepts a return destination inside that tenant. Unknown tenant slugs return 404.

`/auth/login` without a destination is neutral. After OTP verification, `/auth/workspaces` lists active memberships. A short-lived HttpOnly cookie holds any identity token awaiting tenant selection; it is not used as an application session. Profile → Switch workspace opens the same picker.

Selection is a POST. The API obtains the user ID from the authenticated principal, checks an active application membership, and asks IBeam to independently validate identity membership and issue a tenant session. The frontend validates the returned token with the API and checks the selected tenant before replacing access and refresh cookies. Existing route and module authorization remains enforced. Separate browser tabs share the selected session; opening a previous tenant cannot grant access to that tenant.

Tests: `npm run check`, `npm run test:session`, `npx playwright test --config playwright.people.config.ts`, and `AuthWorkspacesTests` in the API authorization test project. Browser fixtures do not send SMS or email.
