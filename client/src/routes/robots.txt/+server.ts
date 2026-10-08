import { getProductionDomain } from '$lib/config/domains';
export const GET = ({ url }) => {
 const content = getProductionDomain(url.hostname)?.surface === 'carlzipf-public'
  ? 'User-agent: *\nAllow: /\nDisallow: /carlzipf/admin/\nDisallow: /carlzipf/tech/\nDisallow: /carlzipf/portal/\nDisallow: /carlzipf/estimate/\nSitemap: https://carlzipflockshop.com/sitemap.xml\n'
  : '# allow crawling everything by default\nUser-agent: *\nDisallow:\n';
 return new Response(content, { headers: { 'Content-Type': 'text/plain; charset=utf-8' } });
};
