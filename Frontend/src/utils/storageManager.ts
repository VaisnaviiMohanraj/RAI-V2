/**
 * Storage Manager for handling localStorage quota and cleanup
 * Prevents Android storage quota issues
 */

export class StorageManager {
  /**
   * Check available storage quota
   */
  static async checkQuota(): Promise<{ available: boolean; usage: number; quota: number }> {
    if ('storage' in navigator && 'estimate' in navigator.storage) {
      try {
        const estimate = await navigator.storage.estimate();
        const usage = estimate.usage || 0;
        const quota = estimate.quota || 0;
        const usagePercent = quota > 0 ? usage / quota : 0;
        
        return { 
          available: usagePercent < 0.9, 
          usage,
          quota
        };
      } catch (error) {
        console.warn('Storage estimate failed:', error);
        return { available: true, usage: 0, quota: 0 };
      }
    }
    return { available: true, usage: 0, quota: 0 };
  }

  /**
   * Clear old chat sessions to free up space
   * Keeps only the most recent sessions
   */
  static clearOldSessions(keepCount: number = 10): number {
    try {
      const sessionsStr = localStorage.getItem('chatSessions');
      if (!sessionsStr) return 0;

      const sessions = JSON.parse(sessionsStr);
      if (!Array.isArray(sessions) || sessions.length <= keepCount) return 0;

      const toKeep = sessions.slice(0, keepCount);
      const toDelete = sessions.slice(keepCount);
      
      // Delete old session data
      toDelete.forEach((session: any) => {
        try {
          localStorage.removeItem(`chat_${session.id}`);
        } catch (e) {
          console.warn('Failed to delete session:', session.id);
        }
      });
      
      // Update sessions list
      localStorage.setItem('chatSessions', JSON.stringify(toKeep));
      
      return toDelete.length;
    } catch (error) {
      console.error('Failed to clear old sessions:', error);
      return 0;
    }
  }

  /**
   * Check if localStorage is available (fails in private/incognito mode)
   */
  static isAvailable(): boolean {
    try {
      const test = '__storage_test__';
      localStorage.setItem(test, test);
      localStorage.removeItem(test);
      return true;
    } catch (e) {
      return false;
    }
  }

  /**
   * Safe localStorage setItem with quota handling
   */
  static safeSetItem(key: string, value: string): boolean {
    try {
      localStorage.setItem(key, value);
      return true;
    } catch (e: any) {
      // QuotaExceededError
      if (e.name === 'QuotaExceededError' || e.code === 22) {
        console.warn('Storage quota exceeded, clearing old sessions...');
        this.clearOldSessions(5); // Keep only 5 most recent
        
        try {
          localStorage.setItem(key, value);
          return true;
        } catch (retryError) {
          console.error('Storage still full after cleanup');
          return false;
        }
      }
      console.error('Failed to save to localStorage:', e);
      return false;
    }
  }

  /**
   * Get storage usage info for debugging
   */
  static async getStorageInfo(): Promise<string> {
    const quota = await this.checkQuota();
    const available = this.isAvailable();
    
    const usageMB = (quota.usage / (1024 * 1024)).toFixed(2);
    const quotaMB = (quota.quota / (1024 * 1024)).toFixed(2);
    
    return `Storage: ${available ? 'Available' : 'Blocked'} | Used: ${usageMB}MB / ${quotaMB}MB`;
  }
}
