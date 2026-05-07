// See https://svelte.dev/docs/kit/types#app.d.ts
// for information about these interfaces
declare global {
	namespace App {
		// interface Error {}
		interface Locals {
			adminSession?: {
				surface: 'external-admin' | 'internal-admin';
				role: 'owner' | 'office-admin' | null;
				email: string;
				tenantId: string;
				source: 'auth-token' | 'contact-access';
			};
			bdrAdminSession?: {
				role: 'owner' | 'office-admin';
				source: 'auth-token' | 'contact-access';
			};
		}
		// interface PageData {}
		// interface PageState {}
		// interface Platform {}
	}
}

export {};
