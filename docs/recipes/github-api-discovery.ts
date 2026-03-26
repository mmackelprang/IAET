import { chromium } from 'playwright';

const browser = await chromium.connectOverCDP(process.env.CDP_ENDPOINT || 'ws://127.0.0.1:9222');
const context = browser.contexts()[0];
const page = context.pages()[0];

await page.goto('https://github.com');
await page.waitForTimeout(2000);

// Search for a repo
await page.fill('[name="q"]', 'playwright');
await page.press('[name="q"]', 'Enter');
await page.waitForTimeout(3000);

// Click first result
await page.click('.repo-list-item a:first-child');
await page.waitForTimeout(3000);

await browser.close();
