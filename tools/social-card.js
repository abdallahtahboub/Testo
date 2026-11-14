// Renders src/Testo.Website/Branding/social-card.html to the PNG used for og:image.
//
// A picture of our own markup, so the card cannot drift away from the brand it is supposed to
// show: the colours are the design tokens and the mark is the one in TBrand.
//
//   node tools/social-card.js
//
// It uses the same browser the tests use. Set TESTO_BROWSER_PATH if it lives somewhere unusual.

const { chromium } = require('playwright');
const path = require('node:path');

const source = path.join(__dirname, '..', 'src', 'Testo.Website', 'Branding', 'social-card.html');
const target = path.join(__dirname, '..', 'src', 'Testo.Web', 'wwwroot', 'img', 'social-card.png');

(async () => {
  const browser = await chromium.launch({
    executablePath: process.env.TESTO_BROWSER_PATH || undefined,
    args: ['--no-sandbox'],
  });

  // 1200 by 630 is what the social platforms ask for, and deviceScaleFactor 1 keeps it exactly
  // that rather than four times the file size for no gain.
  const page = await browser.newPage({ viewport: { width: 1200, height: 630 }, deviceScaleFactor: 1 });
  await page.goto('file://' + source);
  await page.screenshot({ path: target });
  await browser.close();

  console.log('Wrote ' + target);
})();
