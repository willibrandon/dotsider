// @ts-check
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';
import { resolve, join } from 'node:path';
import { existsSync, unlinkSync } from 'node:fs';
import sitemap from '@astrojs/sitemap';

// Workaround for https://github.com/withastro/astro/issues/13464
// Astro's content collection cache (data-store.json) gets stale on
// Windows when content files are renamed or change extension. This
// plugin deletes only the data store — not the whole .astro/ dir,
// which the running server needs.
function contentCacheReload() {
    const dataStore = join(resolve('.astro'), 'data-store.json');
    return {
        name: 'content-cache-reload',
        configureServer(/** @type {any} */ server) {
            for (const event of ['add', 'unlink']) {
                server.watcher.on(event, (/** @type {string} */ path) => {
                    if (!path.includes('src/content') && !path.includes('src\\content')) return;
                    if (existsSync(dataStore)) {
                        unlinkSync(dataStore);
                        console.log(`[content-cache-reload] deleted data-store.json (${event}: ${path.split(/[/\\]/).pop()})`);
                    }
                    server.ws.send({ type: 'full-reload' });
                });
            }
        },
    };
}

export default defineConfig({
    site: 'https://dotsider.dev',
    prefetch: { defaultStrategy: 'hover', prefetchAll: true },
    vite: { plugins: [contentCacheReload()] },
    integrations: [starlight({
        title: 'dotsider',
        favicon: '/favicon.ico',
        disable404Route: true,
        social: [
            { icon: 'github', label: 'GitHub', href: 'https://github.com/willibrandon/dotsider' },
        ],
        head: [
            { tag: 'script', attrs: { src: '/lightbox.js', defer: true } },
            {
                tag: 'link',
                attrs: {
                    rel: 'sitemap',
                    type: 'application/xml',
                    href: '/sitemap-index.xml',
                },
            },
        ],
        components: {
            ThemeProvider: './src/components/ThemeProvider.astro',
            ThemeSelect: './src/components/ThemeSelect.astro',
        },
        customCss: ['./src/styles/custom.css'],
        sidebar: [
            {
                label: 'Getting Started',
                items: [
                    { label: 'Installation', slug: 'getting-started/installation' },
                    { label: 'Quick Start', slug: 'getting-started/quick-start' },
                ],
            },
            {
                label: 'Usage',
                items: [
                    { label: 'General', slug: 'usage/general' },
                    { label: 'PE / Metadata', slug: 'usage/pe-metadata' },
                    { label: 'IL Inspector', slug: 'usage/il-inspector' },
                    { label: 'Strings', slug: 'usage/strings' },
                    { label: 'Hex Dump', slug: 'usage/hex-dump' },
                    { label: 'Dep Graph', slug: 'usage/dep-graph' },
                    { label: 'Size Map', slug: 'usage/size-map' },
                    { label: 'Dynamic', slug: 'usage/dynamic' },
                    { label: 'Diff Mode', slug: 'usage/diff-mode' },
                    { label: 'NuGet Mode', slug: 'usage/nuget-mode' },
                ],
            },
            {
                label: 'Try It',
                items: [
                    { label: 'Live Demo', slug: 'demo' },
                ],
            },
            {
                label: 'Reference',
                items: [
                    { label: 'CLI Reference', slug: 'reference/cli' },
                    { label: 'Keyboard Shortcuts', slug: 'reference/keyboard' },
                    { label: 'MCP Server', slug: 'reference/mcp' },
                ],
            },
            {
                label: 'API Reference',
                autogenerate: { directory: 'api' },
            },
        ],
		}), sitemap()],
});
