/**
 * Test: Homepage Load Validation (Playwright Version)
 *
 * This is a standalone executable test that can be run without Claude Code.
 * It uses Playwright for browser automation.
 *
 * Prerequisites:
 * - pjx-web-react must be running on http://localhost:3000/
 * - Playwright must be installed: npm run install-playwright
 *
 * Usage:
 * npm run test:playwright
 * Or directly: node tests/test-homepage-playwright.js
 */

const { chromium } = require('@playwright/test');

// Test configuration
const TEST_URL = 'http://localhost:3000/';
const TEST_NAME = 'Homepage Load Validation';

// Expected page elements to verify
const EXPECTED_ELEMENTS = {
  pageTitle: 'React App',
  mainHeading: 'Welcome to mikelau13 Demo Website',
  welcomeMessage: 'Welcome!',
  navigationLinks: ['Home', 'Register', 'Sign On'],
  copyright: 'Mike Lau'
};

console.log('========================================');
console.log('PJX Test Automation - Homepage Test');
console.log('========================================\n');

async function runTest() {
  let browser = null;
  let page = null;

  try {
    console.log(`Test: ${TEST_NAME}`);
    console.log(`Target URL: ${TEST_URL}\n`);

    // Step 1: Launch browser
    console.log('Step 1: Launching browser...');
    browser = await chromium.launch({ headless: false });
    const context = await browser.newContext();
    page = await context.newPage();
    console.log('✓ Browser launched successfully\n');

    // Step 2: Navigate to URL
    console.log('Step 2: Navigating to application...');
    await page.goto(TEST_URL, { waitUntil: 'networkidle' });
    console.log(`✓ Navigated to ${TEST_URL}\n`);

    // Step 3: Wait for page to load
    console.log('Step 3: Waiting for page to load...');
    await page.waitForLoadState('domcontentloaded');
    console.log('✓ Page loaded\n');

    // Step 4: Verify page title
    console.log('Step 4: Verifying page elements...');
    const pageTitle = await page.title();
    console.log(`   Page Title: "${pageTitle}"`);

    if (pageTitle === EXPECTED_ELEMENTS.pageTitle) {
      console.log(`   ✓ Page title matches expected: "${EXPECTED_ELEMENTS.pageTitle}"`);
    } else {
      throw new Error(`Page title mismatch. Expected: "${EXPECTED_ELEMENTS.pageTitle}", Got: "${pageTitle}"`);
    }

    // Step 5: Verify main heading
    const mainHeading = await page.locator('h1').first().textContent();
    console.log(`   Main Heading: "${mainHeading}"`);

    if (mainHeading === EXPECTED_ELEMENTS.mainHeading) {
      console.log(`   ✓ Main heading matches expected: "${EXPECTED_ELEMENTS.mainHeading}"`);
    } else {
      throw new Error(`Main heading mismatch. Expected: "${EXPECTED_ELEMENTS.mainHeading}", Got: "${mainHeading}"`);
    }

    // Step 6: Verify welcome message
    const welcomeText = await page.getByText(EXPECTED_ELEMENTS.welcomeMessage).isVisible();
    if (welcomeText) {
      console.log(`   ✓ Welcome message found: "${EXPECTED_ELEMENTS.welcomeMessage}"`);
    } else {
      throw new Error(`Welcome message not found: "${EXPECTED_ELEMENTS.welcomeMessage}"`);
    }

    // Step 7: Verify navigation links
    console.log('   Checking navigation links...');
    for (const linkText of EXPECTED_ELEMENTS.navigationLinks) {
      const link = await page.getByRole('link', { name: linkText }).isVisible();
      if (link) {
        console.log(`   ✓ Link found: "${linkText}"`);
      } else {
        throw new Error(`Navigation link not found: "${linkText}"`);
      }
    }

    // Step 8: Verify copyright
    const copyrightVisible = await page.getByText(EXPECTED_ELEMENTS.copyright).isVisible();
    if (copyrightVisible) {
      console.log(`   ✓ Copyright found: "${EXPECTED_ELEMENTS.copyright}"`);
    } else {
      throw new Error(`Copyright not found: "${EXPECTED_ELEMENTS.copyright}"`);
    }

    console.log('\n✓ All page elements verified successfully\n');

    // Step 9: Take screenshot
    console.log('Step 5: Taking screenshot...');
    const path = require('path');
    const screenshotPath = path.join(__dirname, 'screenshots', 'homepage-test.png');
    await page.screenshot({
      path: screenshotPath,
      fullPage: true
    });
    console.log(`✓ Screenshot saved to: ${screenshotPath}\n`);

    console.log('========================================');
    console.log('TEST RESULT: PASSED ✓');
    console.log('========================================\n');

    return true;

  } catch (error) {
    console.error('\n========================================');
    console.error('TEST RESULT: FAILED ✗');
    console.error('========================================');
    console.error('\nError:', error.message);
    console.error('\nStack trace:', error.stack);
    return false;

  } finally {
    // Step 10: Cleanup - close browser
    if (browser) {
      console.log('\nStep 6: Cleaning up - closing browser...');
      await browser.close();
      console.log('✓ Browser closed successfully\n');
    }
  }
}

// Run the test
runTest()
  .then(success => {
    process.exit(success ? 0 : 1);
  })
  .catch(error => {
    console.error('Unexpected error:', error);
    process.exit(1);
  });
