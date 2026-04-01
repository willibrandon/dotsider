import type { APIRoute } from 'astro';

const getRobotsTxt = (sitemapUrl: URL) => `User-agent: *
Allow: /

Sitemap: ${sitemapUrl.href}
`;

export const GET: APIRoute = ({ site, url }) => {
    const baseUrl = site ?? new URL(url.origin);
    const sitemapUrl = new URL('/sitemap-index.xml', baseUrl);

    return new Response(getRobotsTxt(sitemapUrl), {
        headers: {
            'Content-Type': 'text/plain; charset=utf-8',
        },
    });
};
