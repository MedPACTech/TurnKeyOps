import { error } from '@sveltejs/kit';
import { getProductionDomain } from '$lib/config/domains';
export const GET = ({ url }) => {
 if (getProductionDomain(url.hostname)?.surface !== 'carlzipf-public' && !url.pathname.startsWith('/carlzipf/')) throw error(404);
 const base = url.pathname.startsWith('/carlzipf/') ? '/carlzipf/public' : '';
 const origin = base ? url.origin : 'https://carlzipflockshop.com';
 const escape = (value: string) => value.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;');
 return new Response(`<?xml version="1.0" encoding="UTF-8"?><urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">${[base ? '' : '/', '/residential'].map(path => `<url><loc>${escape(origin + base + path)}</loc></url>`).join('')}</urlset>`, { headers: { 'Content-Type': 'application/xml; charset=utf-8' } });
};
