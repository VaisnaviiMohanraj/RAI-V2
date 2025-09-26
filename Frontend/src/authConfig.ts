import { Configuration, PopupRequest } from "@azure/msal-browser";

// Read Vite env vars for production; fallback to window origin for dev
const env = import.meta.env;
const getRedirectUri = () => {
    // ALWAYS use production URL - no localhost in any deployed environment
    const productionUrl = "https://testing.rrrealty.ai";
    
    // Only use localhost if explicitly running in local development
    if (typeof window !== "undefined") {
        const origin = window.location.origin;
        
        // If origin contains localhost AND we're in development, use localhost
        if (origin.includes('localhost') && import.meta.env.DEV) {
            console.log("MSAL using redirect URI (local development):", origin);
            return origin;
        }
    }
    
    // For ALL other cases (production builds, Azure deployment, etc.), use production URL
    console.log("MSAL using redirect URI (production):", productionUrl);
    return productionUrl;
};

const CLIENT_ID = (env.VITE_AUTH_CLIENT_ID as string) || "d4c452c4-5324-40ff-b43b-25f3daa2a45c";
const AUTHORITY = (env.VITE_AUTH_AUTHORITY as string) || "https://login.microsoftonline.com/99848873-e61d-44cc-9862-d05151c567ab";
const API_SCOPE = (env.VITE_API_SCOPE as string) || "User.Read";

// MSAL configuration
export const msalConfig: Configuration = {
    auth: {
        clientId: CLIENT_ID,
        authority: AUTHORITY,
        redirectUri: getRedirectUri(),
        postLogoutRedirectUri: getRedirectUri(),
    },
    cache: {
        cacheLocation: "sessionStorage",
        storeAuthStateInCookie: false,
    },
};

// Scopes for login (request API access + openid profile)
export const loginRequest: PopupRequest = {
    scopes: [API_SCOPE, "openid", "profile", "email"],
};

// Optional: Graph config (unused by API calls)
export const graphConfig = {
    graphMeEndpoint: "https://graph.microsoft.com/v1.0/me",
};

// Backend API configuration
export const apiConfig = {
    scopes: [API_SCOPE],
    uri: "/api",
};
