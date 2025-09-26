const puppeteer = require('puppeteer');

class RealtyAITester {
  constructor() {
    this.browser = null;
    this.page = null;
  }

  async launch(options = {}) {
    const defaultOptions = {
      headless: false,
      args: ['--start-maximized'],
      devtools: false,
      ...options
    };

    this.browser = await puppeteer.launch(defaultOptions);
    this.page = await this.browser.newPage();
    
    // Set viewport
    await this.page.setViewport({ width: 1200, height: 800 });
    
    console.log('🚀 Browser launched successfully');
    return this;
  }

  async navigateToApp(url = 'http://localhost:3001') {
    await this.page.goto(url, { waitUntil: 'networkidle0' });
    console.log(`📍 Navigated to ${url}`);
    
    // Wait for the app to load
    await this.page.waitForSelector('h1', { timeout: 10000 });
    return this;
  }

  async testApiConnection() {
    console.log('🧪 Testing API connection...');
    
    try {
      // Click the API test button
      await this.page.click('button:has-text("Test API Connection"), button[style*="background-color: rgb(0, 123, 255)"]');
      
      // Wait for test to complete (look for success or error indicators)
      await this.page.waitForFunction(
        () => {
          const result = document.querySelector('pre');
          return result && result.textContent.includes('API Connection');
        },
        { timeout: 15000 }
      );
      
      // Get test results
      const testResult = await this.page.$eval('pre', el => el.textContent);
      
      if (testResult.includes('✅ API Connection Successful')) {
        console.log('✅ API test passed!');
        return { success: true, result: testResult };
      } else {
        console.log('❌ API test failed!');
        return { success: false, result: testResult };
      }
      
    } catch (error) {
      console.error('❌ Error during API test:', error.message);
      return { success: false, error: error.message };
    }
  }

  async testFileUpload(filePath) {
    console.log('📁 Testing file upload...');
    
    try {
      // Find file input (it might be hidden)
      const fileInput = await this.page.$('input[type="file"]');
      if (fileInput) {
        await fileInput.uploadFile(filePath);
        console.log('✅ File uploaded successfully');
        return { success: true };
      } else {
        console.log('❌ File input not found');
        return { success: false, error: 'File input not found' };
      }
    } catch (error) {
      console.error('❌ Error during file upload:', error.message);
      return { success: false, error: error.message };
    }
  }

  async testChatMessage(message = 'Hello, this is a test message from Puppeteer!') {
    console.log('💬 Testing chat message...');
    
    try {
      // Find chat input
      const chatInput = await this.page.$('input[placeholder*="document"], textarea[placeholder*="document"]');
      
      if (chatInput) {
        await chatInput.type(message);
        
        // Find and click send button
        const sendButton = await this.page.$('button[type="submit"], button:has-text("Send")');
        if (sendButton) {
          await sendButton.click();
          console.log('✅ Chat message sent successfully');
          return { success: true };
        }
      }
      
      console.log('❌ Chat interface not found');
      return { success: false, error: 'Chat interface not found' };
      
    } catch (error) {
      console.error('❌ Error during chat test:', error.message);
      return { success: false, error: error.message };
    }
  }

  async takeScreenshot(name = 'test-screenshot') {
    const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
    const filename = `${name}-${timestamp}.png`;
    
    await this.page.screenshot({ 
      path: filename, 
      fullPage: true 
    });
    
    console.log(`📸 Screenshot saved: ${filename}`);
    return filename;
  }

  async runFullTest() {
    console.log('🏃 Running full test suite...');
    
    const results = {
      navigation: false,
      apiConnection: false,
      screenshots: []
    };

    try {
      // Navigate to app
      await this.navigateToApp();
      results.navigation = true;
      
      // Take initial screenshot
      const initialScreenshot = await this.takeScreenshot('01-initial-load');
      results.screenshots.push(initialScreenshot);
      
      // Test API connection
      const apiTest = await this.testApiConnection();
      results.apiConnection = apiTest.success;
      
      // Take screenshot after API test
      const apiScreenshot = await this.takeScreenshot('02-api-test');
      results.screenshots.push(apiScreenshot);
      
      // Test chat (if available)
      await this.testChatMessage();
      
      // Final screenshot
      const finalScreenshot = await this.takeScreenshot('03-final-state');
      results.screenshots.push(finalScreenshot);
      
      console.log('📊 Test Results:', results);
      return results;
      
    } catch (error) {
      console.error('❌ Test suite failed:', error);
      return { ...results, error: error.message };
    }
  }

  async close() {
    if (this.browser) {
      await this.browser.close();
      console.log('🔒 Browser closed');
    }
  }
}

// Usage examples
async function runTests() {
  const tester = new RealtyAITester();
  
  try {
    await tester.launch({ headless: false }); // Set to true for headless testing
    const results = await tester.runFullTest();
    
    console.log('\n🎯 Final Test Summary:');
    console.log(`Navigation: ${results.navigation ? '✅' : '❌'}`);
    console.log(`API Connection: ${results.apiConnection ? '✅' : '❌'}`);
    console.log(`Screenshots: ${results.screenshots.length} taken`);
    
  } finally {
    await tester.close();
  }
}

// Export for use as module
module.exports = RealtyAITester;

// Run if called directly
if (require.main === module) {
  runTests().catch(console.error);
}
