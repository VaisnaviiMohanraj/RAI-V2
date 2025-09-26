import { useIsAuthenticated, useMsal, useAccount } from '@azure/msal-react';
import { useCallback, useEffect, useState } from 'react';
import { AccountInfo } from '@azure/msal-browser';
import { loginRequest } from '../authConfig';

export interface UseAuthReturn {
  isAuthenticated: boolean;
  isLoading: boolean;
  user: AccountInfo | null;
  login: () => Promise<void>;
  logout: () => Promise<void>;
  getAccessToken: () => Promise<string | null>;
  error: string | null;
}

/**
 * Custom hook for managing authentication state and operations
 */
export const useAuth = (): UseAuthReturn => {
  const { instance, accounts } = useMsal();
  const isAuthenticated = useIsAuthenticated();
  const account = useAccount(accounts[0] || {});
  
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Clear error when authentication state changes
  useEffect(() => {
    setError(null);
  }, [isAuthenticated]);

  const login = useCallback(async () => {
    try {
      setIsLoading(true);
      setError(null);
      
      await instance.loginPopup(loginRequest);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Login failed';
      setError(errorMessage);
      console.error('Login error:', err);
    } finally {
      setIsLoading(false);
    }
  }, [instance]);

  const logout = useCallback(async () => {
    try {
      setIsLoading(true);
      setError(null);
      
      await instance.logoutPopup({
        postLogoutRedirectUri: window.location.origin,
        mainWindowRedirectUri: window.location.origin,
      });
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Logout failed';
      setError(errorMessage);
      console.error('Logout error:', err);
    } finally {
      setIsLoading(false);
    }
  }, [instance]);

  const getAccessToken = useCallback(async (): Promise<string | null> => {
    if (!account) {
      return null;
    }

    try {
      const response = await instance.acquireTokenSilent({
        ...loginRequest,
        account: account,
      });
      
      return response.accessToken;
    } catch (err) {
      console.error('Token acquisition error:', err);
      
      // Try interactive token acquisition as fallback
      try {
        const response = await instance.acquireTokenPopup({
          ...loginRequest,
          account: account,
        });
        
        return response.accessToken;
      } catch (interactiveErr) {
        console.error('Interactive token acquisition error:', interactiveErr);
        setError('Failed to acquire access token');
        return null;
      }
    }
  }, [instance, account]);

  return {
    isAuthenticated,
    isLoading,
    user: account,
    login,
    logout,
    getAccessToken,
    error,
  };
};
