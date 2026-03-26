import { chromium } from 'playwright';

// Connect to IAET's browser instance
const browser = await chromium.connectOverCDP(process.env.CDP_ENDPOINT || 'ws://127.0.0.1:9222');
const context = browser.contexts()[0];
const page = context.pages()[0];

// Navigate to Spotify
await page.goto('https://open.spotify.com');
await page.waitForTimeout(3000);

// Search for a playlist
await page.fill('[data-testid="search-input"]', 'Top 50');
await page.waitForTimeout(2000);

// Click first result
await page.click('[data-testid="search-result"]:first-child');
await page.waitForTimeout(3000);

// Tag this interaction
await page.evaluate(() => (window as any).__iaet_tag = 'playlist-view');

// Play a track
await page.click('[data-testid="play-button"]');
await page.waitForTimeout(5000);

await browser.close();
