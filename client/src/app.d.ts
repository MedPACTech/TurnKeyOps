// See https://svelte.dev/docs/kit/types#app.d.ts
// for information about these interfaces
declare global {
	namespace App {
		// interface Error {}
		interface Locals {
			adminSession?: {
				surface: 'external-admin' | 'internal-admin';
				role: 'owner' | 'office-admin' | 'estimator-crew-lite' | null;
				email: string;
				tenantId: string;
				source: 'auth-token';
			};
			bdrAdminSession?: {
				role: 'owner' | 'office-admin' | 'estimator-crew-lite';
				source: 'auth-token';
			};
		}
		// interface PageData {}
		// interface PageState {}
		// interface Platform {}
	}
}

export {};
