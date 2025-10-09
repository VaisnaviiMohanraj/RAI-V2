import { Configuration, PopupRequest } from "@azure/msal-browser";

// Read Vite env vars for production; fallback to window origin for dev
const env = import.meta.env;
const getRedirectUri = () => {
    // Check multiple conditions to determine if we're in local development
    const isLocalDevelopment = typeof window !== "undefined" && 
                              window.location.origin.includes('localhost') && 
                              env.DEV === true && 
                              env.MODE === 'development' &&
                              // Additional safety check - not in production environment
                              env.NODE_ENV !== 'production';
    
    // Only use localhost if ALL conditions are met for local development
    if (isLocalDevelopment) {
        const origin = window.location.origin;
        console.log("MSAL using redirect URI (local development):", origin);
        return origin;
    }
    
    // For production/deployed builds, use the current window origin
    const origin = typeof window !== "undefined" ? window.location.origin : "https://www.rrrealty.ai";
    console.log("MSAL using redirect URI (production):", origin);
    return origin;
};

const CLIENT_ID = env.VITE_AUTH_CLIENT_ID || "d4c452c4-5324-40ff-b43b-25f3daa2a45c";
const AUTHORITY = env.VITE_AUTH_AUTHORITY || "https://login.microsoftonline.com/99848873-e61d-44cc-9862-d05151c567ab";
const API_SCOPE = env.VITE_API_SCOPE || `api://${CLIENT_ID}/access_as_user`;

// MSAL configuration
export const msalConfig: Configuration = {
    auth: {
        clientId: CLIENT_ID,
        authority: AUTHORITY,
        redirectUri: getRedirectUri(),
        postLogoutRedirectUri: getRedirectUri(),
    },
    cache: {
        cacheLocation: "localStorage",
        storeAuthStateInCookie: true,
    },
    system: {
        allowRedirectInIframe: false,
        windowHashTimeout: 9000,
        iframeHashTimeout: 9000,
        loadFrameTimeout: 9000,
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
