export const adminModule = (pathname: string): string | null => {
 const match = pathname.match(/^\/(bdr|thinkpink|carlzipf)\/admin(?:\/([^/]+))?/);
 if (!match) return null;
 const slug = match[2] || 'bob';
 return ({'customer-records':'contacts',contact:'contacts',customers:'contacts',website:'settings',content:'settings'} as Record<string,string>)[slug] || slug;
};
export const hasModuleAccess = (permissions: string[], module: string, write = false) => {
 if(module==='intelligence')return permissions.includes('bob.read')&&(!write||permissions.includes('bob.write'));
 if (!permissions.includes(`${module}.read`) || (write && !permissions.includes(`${module}.write`))) return false;
 if (module==='dashboard') return ['calendar','jobs','requests','estimates','invoices','contacts'].every(m=>permissions.includes(`${m}.read`));
 return true;
};

export const firstAllowedAdminPage = (permissions: string[]): string | null => {
 for (const module of ['bob','intelligence','dashboard','leads','jobs','inventory','purchasing','calendar','requests','estimates','invoices','finance','payroll','contacts','users','settings']) {
  if (hasModuleAccess(permissions,module)) return module === 'contacts' ? 'customers' : module;
 }
 return null;
};
