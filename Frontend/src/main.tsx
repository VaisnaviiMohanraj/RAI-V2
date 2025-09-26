import React from 'react'
import ReactDOM from 'react-dom/client'
import { MsalProvider } from '@azure/msal-react'
import { authService } from './services/authService'
import App from './App.tsx'
import './index.css'

// Initialize MSAL and handle redirects
authService.initialize().then(async () => {
  // Handle redirect responses (for authentication callbacks)
  try {
    const response = await authService.getInstance().handleRedirectPromise();
    if (response) {
      console.log('Authentication successful:', response);
      // Set active account if not already set
      if (!authService.getInstance().getActiveAccount() && response.account) {
        authService.getInstance().setActiveAccount(response.account);
      }
    }
  } catch (error) {
    console.error('Error handling redirect:', error);
  }

  ReactDOM.createRoot(document.getElementById('root')!).render(
    <React.StrictMode>
      <MsalProvider instance={authService.getInstance()}>
        <App />
      </MsalProvider>
    </React.StrictMode>,
  )
}).catch(error => {
  console.error('Failed to initialize MSAL:', error);
})
