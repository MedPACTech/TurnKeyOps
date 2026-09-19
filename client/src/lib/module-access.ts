export const adminModule = (pathname: string): string | null => {
 const match = pathname.match(/^\/(bdr|thinkpink)\/admin(?:\/([^/]+))?/);
 if (!match) return null;
 const slug = match[2] || 'bob';
 return ({contact:'users',customers:'contacts',website:'settings',content:'settings'} as Record<string,string>)[slug] || slug;
};
export const hasModuleAccess = (permissions: string[], module: string, write = false) => {
 if (!permissions.includes(`${module}.read`) || (write && !permissions.includes(`${module}.write`))) return false;
 if (module==='dashboard' || module==='bob') return ['calendar','jobs','requests','estimates','invoices','contacts'].every(m=>permissions.includes(`${m}.read`) && (module!=='bob' || permissions.includes(`${m}.write`)));
 return true;
};

export const firstAllowedAdminPage = (permissions: string[]): string | null => {
 for (const module of ['bob','dashboard','jobs','calendar','requests','estimates','invoices','contacts','users','settings']) {
  if (hasModuleAccess(permissions,module)) return module === 'contacts' ? 'customers' : module;
 }
 return null;
};
