# PJX Test Automation

Test automation framework for the PJX project with support for both Playwright (standalone) and Chrome DevTools MCP (via Claude Code).

## Table of Contents

- [Project Structure](#project-structure)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Running Tests](#running-tests)
- [Test Cases](#test-cases)
- [Using Chrome DevTools MCP](#using-chrome-devtools-mcp)
- [Development Guidelines](#development-guidelines)
- [Troubleshooting](#troubleshooting)

## Project Structure

```text
pjx-test-automation/
├── tests/
│   ├── test-homepage.js            # MCP version: For use with Claude Code
│   ├── test-homepage-playwright.js # Standalone: Can run independently
│   └── screenshots/                # Test screenshots
├── package.json                     # Project dependencies and scripts
└── README.md                       # This file
```

## Prerequisites

1. **Node.js** - Ensure Node.js is installed (v16 or higher recommended)
2. **pjx-web-react** - The React application must be running on <http://localhost:3000/>

## Getting Started

### First-Time Setup

#### Option 1: Playwright (Standalone Testing)

If you want to run tests independently without Claude Code:

```bash
# Navigate to the test automation directory
cd projects/pjx-test-automation

# Install Playwright and dependencies
npm run install-playwright
```

This will:

- Install `@playwright/test` package
- Download Chromium browser
- Set up the testing environment

#### Option 2: Chrome DevTools MCP (Claude Code)

If you want to use Chrome DevTools MCP via Claude Code:

1. Chrome DevTools MCP should already be configured in your Claude Code setup
2. No additional installation needed
3. Just run the MCP version of the test (see [Running Tests](#running-tests))

## Running Tests

### Method 1: Standalone with Playwright (No Claude Code Required)

**Quick Start:**

```bash
cd projects/pjx-test-automation
npm run test:playwright
```

**Alternative (Direct Execution):**

```bash
node tests/test-homepage-playwright.js
```

**What This Does:**

- Opens Chrome browser (visible, non-headless mode)
- Navigates to <http://localhost:3000/>
- Verifies all expected page elements:
  - Page title
  - Main heading
  - Welcome message
  - Navigation links (Home, Register, Sign On)
  - Copyright information
- Takes a full-page screenshot
- Saves screenshot to `tests/screenshots/homepage-test.png`
- Closes the browser automatically
- Exits with appropriate status code (0 = pass, 1 = fail)

**Expected Output:**

```text
========================================
PJX Test Automation - Homepage Test
========================================

Test: Homepage Load Validation
Target URL: http://localhost:3000/

Step 1: Launching browser...
✓ Browser launched successfully

Step 2: Navigating to application...
✓ Navigated to http://localhost:3000/

Step 3: Waiting for page to load...
✓ Page loaded

Step 4: Verifying page elements...
   Page Title: "React App"
   ✓ Page title matches expected
   Main Heading: "Welcome to mikelau13 Demo Website"
   ✓ Main heading matches expected
   ✓ Welcome message found
   ✓ Link found: "Home"
   ✓ Link found: "Register"
   ✓ Link found: "Sign On"
   ✓ Copyright found

✓ All page elements verified successfully

Step 5: Taking screenshot...
✓ Screenshot saved to: tests/screenshots/homepage-test.png

========================================
TEST RESULT: PASSED ✓
========================================

Step 6: Cleaning up - closing browser...
✓ Browser closed successfully
```

### Method 2: With Chrome DevTools MCP via Claude Code

**Display Test Guide:**

```bash
cd projects/pjx-test-automation
npm test
```

This displays instructions for running the test with Chrome DevTools MCP.

**Run Test via Claude Code:**

1. Ensure `pjx-web-react` is running on <http://localhost:3000/>
2. In Claude Code, say: **"Run the homepage test using Chrome DevTools MCP"**
3. Claude will execute the following steps:
   - Open browser: `mcp__chrome-devtools__new_page({url: "http://localhost:3000/"})`
   - Capture content: `mcp__chrome-devtools__take_snapshot()`
   - Verify expected elements in snapshot
   - Take screenshot: `mcp__chrome-devtools__take_screenshot()`
   - List pages: `mcp__chrome-devtools__list_pages()`
   - Close browser: `mcp__chrome-devtools__close_page({pageIdx: <index>})`

**Advantages of MCP Method:**

- Interactive testing with Claude's assistance
- Real-time analysis and validation
- Natural language test commands
- No additional dependencies beyond MCP setup

## Test Cases

### 1. Homepage Load Validation

**Files:**

- MCP Version: `test-homepage.js`
- Playwright Version: `test-homepage-playwright.js`

**Purpose:** Validates that the pjx-web-react application homepage loads successfully and displays all expected content.

**Test Flow:**

1. Open browser and navigate to URL
2. Take snapshot of page content
3. Verify key page elements are present
4. Take screenshot for visual confirmation
5. Close browser (cleanup)

**Expected Elements:**

- **Page Title:** "React App"
- **Main Heading:** "Welcome to mikelau13 Demo Website"
- **Welcome Message:** "Welcome!"
- **Navigation Links:** Home, Register, Sign On
- **Copyright:** "Mike Lau 2025"

**Expected Result:** Homepage loads successfully and displays correctly with all elements present.

## Using Chrome DevTools MCP

The Chrome DevTools MCP provides powerful browser automation capabilities when using Claude Code.

### Available MCP Tools

| Tool | Description |
|------|-------------|
| `mcp__chrome-devtools__new_page` | Open a new browser page and navigate to URL |
| `mcp__chrome-devtools__navigate_page` | Navigate the current page to a new URL |
| `mcp__chrome-devtools__take_snapshot` | Take a text snapshot of page structure |
| `mcp__chrome-devtools__take_screenshot` | Capture a visual screenshot |
| `mcp__chrome-devtools__list_pages` | List all open browser pages |
| `mcp__chrome-devtools__close_page` | Close a specific page |
| `mcp__chrome-devtools__click` | Click on page elements |
| `mcp__chrome-devtools__fill` | Fill form inputs |
| `mcp__chrome-devtools__evaluate_script` | Execute JavaScript in the page |

### Example Usage via Claude Code

**Step-by-step Example:**

```text
You: "Open a new page at http://localhost:3000/ using Chrome DevTools"
Claude: [Uses mcp__chrome-devtools__new_page to open browser]

You: "Take a snapshot of the current page"
Claude: [Uses mcp__chrome-devtools__take_snapshot to capture content]

You: "Verify the page loaded correctly"
Claude: [Analyzes snapshot and validates elements]

You: "Take a screenshot"
Claude: [Uses mcp__chrome-devtools__take_screenshot]

You: "Close the browser page"
Claude: [Uses mcp__chrome-devtools__close_page]
```

**Complete Test Example:**

```text
You: "Run the homepage test using Chrome DevTools MCP"
Claude will automatically:
✓ Open browser at http://localhost:3000/
✓ Take snapshot
✓ Verify all expected elements
✓ Take screenshot
✓ Close browser
✓ Report results
```

## Development Guidelines

### Adding New Tests

1. **Create a new test file** in the `tests/` directory
2. **Follow naming convention:** `test-[feature-name].js` or `test-[feature-name]-playwright.js`
3. **Include clear documentation** at the top of the file
4. **Use descriptive console output** for test steps
5. **Always include cleanup** (close browser) in `finally` block

### Test Structure Template (Playwright)

```javascript
/**
 * Test: [Test Name]
 * Description: [What this test validates]
 * Prerequisites: [Required setup]
 */

const { chromium } = require('@playwright/test');

const TEST_URL = 'http://localhost:3000/path';
const TEST_NAME = '[Test Name]';

async function runTest() {
  let browser = null;

  try {
    console.log(`Test: ${TEST_NAME}`);

    // Launch browser
    browser = await chromium.launch({ headless: false });
    const context = await browser.newContext();
    const page = await context.newPage();

    // Navigate
    await page.goto(TEST_URL, { waitUntil: 'networkidle' });

    // Test steps
    // ... your test logic here

    // Take screenshot
    await page.screenshot({ path: 'tests/screenshots/test-name.png' });

    console.log('TEST PASSED ✓');
    return true;

  } catch (error) {
    console.error('TEST FAILED ✗:', error.message);
    return false;

  } finally {
    // Always cleanup
    if (browser) {
      await browser.close();
    }
  }
}

runTest()
  .then(success => process.exit(success ? 0 : 1))
  .catch(error => {
    console.error('Unexpected error:', error);
    process.exit(1);
  });
```

### Test Structure Template (MCP)

```javascript
/**
 * Test: [Test Name]
 * Description: [What this test validates]
 *
 * Usage via Claude Code:
 * Ask Claude: "Run [test name] using Chrome DevTools"
 */

console.log('Test: [Test Name]');

const EXPECTED_ELEMENTS = {
  // Define expected page elements
};

async function runTest() {
  try {
    console.log('Step 1: [Description]...');
    console.log('[INFO] Use mcp__chrome-devtools__[tool_name]');

    // Document each step

    console.log('TEST EXECUTION GUIDE');
    console.log('Ask Claude to execute the test with Chrome DevTools MCP');

    return true;
  } catch (error) {
    console.error('TEST FAILED:', error.message);
    return false;
  } finally {
    console.log('[INFO] Remember to close browser!');
  }
}

runTest()
  .then(success => process.exit(success ? 0 : 1))
  .catch(error => {
    console.error('Unexpected error:', error);
    process.exit(1);
  });
```

### Best Practices

1. **Always close browsers** after tests to avoid resource leaks
2. **Use meaningful test names** that describe what is being tested
3. **Verify expected outcomes** - don't just check if page loads
4. **Take screenshots** for visual confirmation and debugging
5. **Handle errors gracefully** with try-catch-finally blocks
6. **Wait for elements** before interacting with them
7. **Use explicit waits** instead of arbitrary timeouts
8. **Keep tests independent** - each test should work standalone

### Available npm Scripts

| Script | Command | Description |
|--------|---------|-------------|
| `test` | `npm test` | Shows MCP test guide |
| `test:playwright` | `npm run test:playwright` | Runs Playwright test |
| `install-playwright` | `npm run install-playwright` | Installs Playwright + Chromium |

## Troubleshooting

### Application Not Accessible

**Problem:** Test cannot reach <http://localhost:3000/>

**Solutions:**

1. **Verify pjx-web-react is running:**

   ```bash
   # Check Docker containers
   docker ps

   # Or check if port 3000 is listening
   netstat -an | grep 3000
   ```

2. **Test connectivity:**

   ```bash
   curl http://localhost:3000/
   # Should return HTML content
   ```

3. **Check firewall settings:**
   - Ensure no firewall is blocking localhost connections
   - Check if port 3000 is open

4. **Restart the application:**

   ```bash
   # Stop and restart pjx-web-react
   cd ../pjx-web-react
   # Use your normal startup commands
   ```

### Playwright Installation Issues

**Problem:** `npm run install-playwright` fails

**Solutions:**

1. **Check Node.js version:**

   ```bash
   node --version
   # Should be v16 or higher
   ```

2. **Clear npm cache:**

   ```bash
   npm cache clean --force
   npm run install-playwright
   ```

3. **Install manually:**

   ```bash
   npm install @playwright/test
   npx playwright install chromium
   ```

4. **Check disk space:**

   ```bash
   df -h
   # Playwright needs ~300MB for Chromium
   ```

### Chrome DevTools MCP Not Responding

**Problem:** MCP tools are not working in Claude Code

**Solutions:**

1. **Verify MCP is installed:**
   - Check Claude Code settings
   - Look for Chrome DevTools MCP in MCP servers list

2. **Restart Claude Code:**
   - Close and reopen Claude Code
   - Reload MCP servers

3. **Check Chrome installation:**

   ```bash
   google-chrome --version
   # Should show Chrome version
   ```

4. **Check MCP logs:**
   - Look for error messages in Claude Code console
   - Check MCP server status

### Test Fails with "Element Not Found"

**Problem:** Test cannot find expected page elements

**Solutions:**

1. **Check if page fully loaded:**
   - Increase wait time
   - Use `waitUntil: 'networkidle'`

2. **Verify element selectors:**
   - Open browser DevTools
   - Inspect the element
   - Confirm text content matches expected values

3. **Check for dynamic content:**
   - Elements might load asynchronously
   - Add explicit waits for specific elements

4. **Update expected values:**
   - Page content might have changed
   - Update `EXPECTED_ELEMENTS` in test file

### Screenshot Not Saved

**Problem:** Screenshot file not created

**Solutions:**

1. **Check directory exists:**

   ```bash
   mkdir -p tests/screenshots
   ```

2. **Check write permissions:**

   ```bash
   ls -la tests/
   # Ensure you have write permissions
   ```

3. **Check disk space:**

   ```bash
   df -h
   ```

### Browser Doesn't Close

**Problem:** Browser remains open after test

**Solutions:**

1. **Manual cleanup:**

   ```bash
   # Find Chrome processes
   ps aux | grep chrome

   # Kill if necessary
   killall chrome
   ```

2. **Check test code:**
   - Ensure `finally` block is present
   - Verify `browser.close()` is called

3. **For MCP tests:**
   - Use `mcp__chrome-devtools__list_pages`
   - Use `mcp__chrome-devtools__close_page` for each page

## Future Enhancements

- [ ] Add more test cases for different pages (Register, Sign On, etc.)
- [ ] Implement test reporting (HTML reports, JUnit XML)
- [ ] Add screenshot comparison for visual regression testing
- [ ] Create test data fixtures and mocks
- [ ] Add CI/CD integration (GitHub Actions, Jenkins)
- [ ] Implement parallel test execution
- [ ] Add API testing capabilities
- [ ] Create performance testing suite
- [ ] Add accessibility testing (WCAG compliance)
- [ ] Implement test data generators

## Support

For issues or questions:

1. **Check test output logs** for error messages
2. **Review this README** for common solutions
3. **Consult Playwright documentation:** <https://playwright.dev/>
4. **Consult Chrome DevTools MCP documentation** in Claude Code
5. **Check project issues** on GitHub (if applicable)

## Contributing

When adding new tests or features:

1. Follow the existing code structure
2. Add documentation in this README
3. Include both Playwright and MCP versions (if applicable)
4. Test thoroughly before committing
5. Update the version number if needed

---

**Created:** 2025-10-10
**Version:** 1.0.0
**Branch:** feature/test-automation-setup
**Author:** PJX Team
**License:** ISC
