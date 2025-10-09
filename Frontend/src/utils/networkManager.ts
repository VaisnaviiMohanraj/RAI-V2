/**
 * Network Manager for handling mobile network issues
 * Provides retry logic and online/offline detection
 */

export class NetworkManager {
  /**
   * Check if browser is online
   */
  static isOnline(): boolean {
    return navigator.onLine;
  }

  /**
   * Retry a function with exponential backoff
   */
  static async retryFetch<T>(
    fn: () => Promise<T>,
    retries: number = 3,
    delay: number = 1000
  ): Promise<T> {
    try {
      return await fn();
    } catch (error: any) {
      // Don't retry if offline or no retries left
      if (retries === 0 || !this.isOnline()) {
        throw error;
      }
      
      // Don't retry on 4xx errors (client errors)
      if (error.response?.status >= 400 && error.response?.status < 500) {
        throw error;
      }
      
      console.log(`Retry attempt ${4 - retries}/3 after ${delay}ms...`);
      await new Promise(resolve => setTimeout(resolve, delay));
      
      // Exponential backoff
      return this.retryFetch(fn, retries - 1, delay * 2);
    }
  }

  /**
   * Setup listeners for online/offline events
   * Returns cleanup function
   */
  static setupOnlineListener(callback: (online: boolean) => void): () => void {
    const handleOnline = () => {
      console.log('Network: Online');
      callback(true);
    };
    
    const handleOffline = () => {
      console.log('Network: Offline');
      callback(false);
    };
    
    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);
    
    // Return cleanup function
    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
    };
  }

  /**
   * Wait for network to be online
   */
  static async waitForOnline(timeout: number = 30000): Promise<boolean> {
    if (this.isOnline()) return true;

    return new Promise((resolve) => {
      const timeoutId = setTimeout(() => {
        cleanup();
        resolve(false);
      }, timeout);

      const handleOnline = () => {
        cleanup();
        resolve(true);
      };

      const cleanup = () => {
        clearTimeout(timeoutId);
        window.removeEventListener('online', handleOnline);
      };

      window.addEventListener('online', handleOnline);
    });
  }
}
