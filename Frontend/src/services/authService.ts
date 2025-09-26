import { PublicClientApplication, AccountInfo, AuthenticationResult, SilentRequest } from "@azure/msal-browser";
import { msalConfig, loginRequest, apiConfig } from "../authConfig";

class AuthService {
    private msalInstance: PublicClientApplication;

    constructor() {
        this.msalInstance = new PublicClientApplication(msalConfig);
    }

    async initialize(): Promise<void> {
        await this.msalInstance.initialize();
    }

    getInstance(): PublicClientApplication {
        return this.msalInstance;
    }

    async loginPopup(): Promise<AuthenticationResult> {
        try {
            const response = await this.msalInstance.loginPopup(loginRequest);
            return response;
        } catch (error) {
            console.error("Login failed:", error);
            throw error;
        }
    }

    async loginRedirect(): Promise<void> {
        try {
            await this.msalInstance.loginRedirect(loginRequest);
        } catch (error) {
            console.error("Login redirect failed:", error);
            throw error;
        }
    }

    async logout(): Promise<void> {
        try {
            await this.msalInstance.logoutPopup();
        } catch (error) {
            console.error("Logout failed:", error);
            throw error;
        }
    }

    getActiveAccount(): AccountInfo | null {
        return this.msalInstance.getActiveAccount();
    }

    getAllAccounts(): AccountInfo[] {
        return this.msalInstance.getAllAccounts();
    }

    async getAccessToken(): Promise<string | null> {
        const account = this.getActiveAccount();
        if (!account) {
            throw new Error("No active account found");
        }

        const silentRequest: SilentRequest = {
            scopes: apiConfig.scopes,
            account: account
        };

        try {
            const response = await this.msalInstance.acquireTokenSilent(silentRequest);
            return response.accessToken;
        } catch (error) {
            console.error("Silent token acquisition failed:", error);
            // If silent request fails, try popup
            try {
                const response = await this.msalInstance.acquireTokenPopup({
                    scopes: apiConfig.scopes,
                    account: account
                });
                return response.accessToken;
            } catch (popupError) {
                console.error("Popup token acquisition failed:", popupError);
                throw popupError;
            }
        }
    }

    isAuthenticated(): boolean {
        const account = this.getActiveAccount();
        return account !== null;
    }

    getUserInfo(): { name?: string; email?: string; username?: string } | null {
        const account = this.getActiveAccount();
        if (!account) return null;

        return {
            name: account.name,
            email: account.username,
            username: account.username
        };
    }
}

export const authService = new AuthService();
