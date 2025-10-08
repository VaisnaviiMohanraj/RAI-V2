import React, { useEffect, useState } from 'react';
import { useIsAuthenticated, useMsal } from '@azure/msal-react';
import { authService } from '../../services/authService';
import './AuthWrapper.css';

interface AuthWrapperProps {
  children: React.ReactNode;
}

const AuthWrapper: React.FC<AuthWrapperProps> = ({ children }) => {
  const isAuthenticated = useIsAuthenticated();
  const { accounts } = useMsal();
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const initializeAuth = async () => {
      try {
        if (isAuthenticated && accounts.length > 0) {
          // Set active account if not already set
          const msalInstance = authService.getInstance();
          if (!msalInstance.getActiveAccount() && accounts.length > 0) {
            msalInstance.setActiveAccount(accounts[0]);
          }
        }
      } catch (error) {
        console.error('Auth initialization error:', error);
      } finally {
        setIsLoading(false);
      }
    };

    initializeAuth();
  }, [isAuthenticated, accounts]);

  const handleLogin = async () => {
    try {
      setIsLoading(true);
      await authService.loginRedirect();
    } catch (error) {
      console.error('Login error:', error);
    } finally {
      setIsLoading(false);
    }
  };


  if (isLoading) {
    return (
      <div className="auth-loading">
        <div className="loading-spinner"></div>
        <p>Initializing authentication...</p>
      </div>
    );
  }

  if (!isAuthenticated) {
    return (
      <div className="auth-container">
        <div className="auth-card">
          <div className="auth-header">
            <h1>R&R Realty AI</h1>
            <p>Sign in to access your AI assistant</p>
          </div>
          
          <div className="auth-content">
            <button 
              className="login-button"
              onClick={handleLogin}
              disabled={isLoading}
            >
              {isLoading ? 'Signing in...' : 'Sign in with Microsoft'}
            </button>
            
            <div className="auth-info">
              <p>Use your organizational account to sign in</p>
            </div>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="authenticated-app">
      {children}
    </div>
  );
};

export default AuthWrapper;
