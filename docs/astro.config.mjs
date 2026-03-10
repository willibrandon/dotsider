// @ts-check
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

export default defineConfig({
	integrations: [
		starlight({
			title: 'dotsider',
			favicon: '/favicon.ico',
			social: [
				{ icon: 'github', label: 'GitHub', href: 'https://github.com/willibrandon/dotsider' },
			],
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
						{ label: 'General Tab', slug: 'usage/general' },
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
				{
					label: 'Try It',
					items: [
						{ label: 'Live Demo', slug: 'demo' },
					],
				},
			],
		}),
	],
});
