import { Configuration, PopupRequest } from "@azure/msal-browser";

// Read Vite env vars for production; fallback to window origin for dev
const env = import.meta.env;
const getRedirectUri = () => {
    // ALWAYS use production URL - no localhost in any deployed environment
    const productionUrl = "https://testing.rrrealty.ai";
    
    // Force production URL if explicitly set via environment variable
    if (env.VITE_FORCE_PRODUCTION_URL === 'true') {
        console.log("MSAL using redirect URI (forced production):", productionUrl);
        return productionUrl;
    }
    
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
        console.log("Environment details:", {
            DEV: env.DEV,
            MODE: env.MODE,
            NODE_ENV: env.NODE_ENV,
            origin: origin
        });
        return origin;
    }
    
    // For ALL other cases (production builds, Azure deployment, etc.), use production URL
    console.log("MSAL using redirect URI (production):", productionUrl);
    console.log("Production environment details:", {
        DEV: env.DEV,
        MODE: env.MODE,
        NODE_ENV: env.NODE_ENV,
        origin: typeof window !== "undefined" ? window.location.origin : "server-side"
    });
    return productionUrl;
};

const CLIENT_ID = env.VITE_AUTH_CLIENT_ID || "d4c452c4-5324-40ff-b43b-25f3daa2a45c";
const AUTHORITY = env.VITE_AUTH_AUTHORITY || "https://login.microsoftonline.com/99848873-e61d-44cc-9862-d05151c567ab";
const API_SCOPE = env.VITE_API_SCOPE || "User.Read";

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
