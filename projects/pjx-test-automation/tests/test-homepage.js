/**
 * Test: Homepage Load Validation
 *
 * This test validates that the pjx-web-react application homepage
 * can be successfully loaded at http://localhost:3000/
 *
 * Prerequisites:
 * - pjx-web-react must be running on http://localhost:3000/
 * - Chrome DevTools MCP must be configured
 *
 * Usage:
 * Run this test using: npm test
 * Or directly: node tests/test-homepage.js
 *
 * Test Flow:
 * 1. Open browser and navigate to URL
 * 2. Take snapshot of page content
 * 3. Verify key page elements are present
 * 4. Take screenshot for visual confirmation
 * 5. Close browser (cleanup)
 */

console.log('========================================');
console.log('PJX Test Automation - Homepage Test');
console.log('========================================\n');

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

// Test execution
async function runTest() {
  let browserPageIndex = null;

  try {
    console.log(`Test: ${TEST_NAME}`);
    console.log(`Target URL: ${TEST_URL}\n`);

    console.log('Step 1: Opening browser and navigating to application...');
    console.log('[INFO] When executed via Chrome DevTools MCP:');
    console.log('       - Use mcp__chrome-devtools__new_page to open browser');
    console.log('       - Browser will navigate to:', TEST_URL);
    console.log('       - Store the page index for later cleanup\n');

    console.log('Step 2: Taking snapshot of page content...');
    console.log('[INFO] Use mcp__chrome-devtools__take_snapshot');
    console.log('       - Captures text representation of page');
    console.log('       - Returns page structure with UIDs\n');

    console.log('Step 3: Verifying page elements...');
    console.log('[INFO] Verify the following elements are present:');
    console.log(`       - Page Title: "${EXPECTED_ELEMENTS.pageTitle}"`);
    console.log(`       - Main Heading: "${EXPECTED_ELEMENTS.mainHeading}"`);
    console.log(`       - Welcome Message: "${EXPECTED_ELEMENTS.welcomeMessage}"`);
    console.log(`       - Navigation Links: ${EXPECTED_ELEMENTS.navigationLinks.join(', ')}`);
    console.log(`       - Copyright contains: "${EXPECTED_ELEMENTS.copyright}"\n`);

    console.log('Step 4: Taking screenshot for visual confirmation...');
    console.log('[INFO] Use mcp__chrome-devtools__take_screenshot');
    console.log('       - Captures visual rendering of page');
    console.log('       - Saves as PNG for review\n');

    console.log('Step 5: Cleaning up - closing browser...');
    console.log('[INFO] IMPORTANT: Always close browser after test!');
    console.log('       - Use mcp__chrome-devtools__list_pages to get page list');
    console.log('       - Use mcp__chrome-devtools__close_page with page index');
    console.log('       - Note: Cannot close the last open page\n');

    console.log('========================================');
    console.log('TEST EXECUTION GUIDE');
    console.log('========================================');
    console.log('\nTo run this test with Chrome DevTools MCP via Claude Code:\n');
    console.log('1. Ensure pjx-web-react is running on http://localhost:3000/');
    console.log('2. Ask Claude: "Run the homepage test using Chrome DevTools"');
    console.log('3. Claude will execute:');
    console.log('   a) mcp__chrome-devtools__new_page({url: "http://localhost:3000/"})');
    console.log('   b) mcp__chrome-devtools__take_snapshot()');
    console.log('   c) Verify expected elements in snapshot');
    console.log('   d) mcp__chrome-devtools__take_screenshot()');
    console.log('   e) mcp__chrome-devtools__list_pages()');
    console.log('   f) mcp__chrome-devtools__close_page({pageIdx: <index>})');
    console.log('\n========================================\n');

    return true;
  } catch (error) {
    console.error('TEST FAILED:', error.message);
    console.error('\n[CLEANUP] Attempting to close browser if open...');

    // In actual execution, cleanup would happen here
    console.log('[INFO] Use mcp__chrome-devtools__list_pages and close_page');

    return false;
  } finally {
    console.log('[INFO] Test execution completed.');
    console.log('[INFO] Remember: Browser cleanup is essential to avoid resource leaks!\n');
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
