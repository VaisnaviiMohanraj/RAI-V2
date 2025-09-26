// Application constants

// API Configuration
export const API_CONFIG = {
  BASE_URL: import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000',
  TIMEOUT: parseInt(import.meta.env.VITE_API_TIMEOUT || '30000'),
  RETRY_ATTEMPTS: 3,
  RETRY_DELAY: 1000,
} as const;

// File Upload Configuration
export const FILE_CONFIG = {
  MAX_SIZE_MB: parseInt(import.meta.env.VITE_MAX_FILE_SIZE_MB || '10'),
  ALLOWED_TYPES: [
    'application/pdf',
    'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
    'application/msword',
    'text/plain',
  ],
  ALLOWED_EXTENSIONS: ['.pdf', '.docx', '.doc', '.txt'],
} as const;

// UI Configuration
export const UI_CONFIG = {
  ANIMATION_DURATION: 200,
  DEBOUNCE_DELAY: 300,
  TOAST_DURATION: 5000,
  MAX_MESSAGE_LENGTH: 4000,
  MESSAGES_PER_PAGE: 50,
} as const;

// Local Storage Keys
export const STORAGE_KEYS = {
  CHAT_SESSIONS: 'rr_realty_chat_sessions',
  USER_PREFERENCES: 'rr_realty_user_preferences',
  THEME: 'rr_realty_theme',
  LAST_SESSION: 'rr_realty_last_session',
} as const;

// Theme Configuration
export const THEME = {
  COLORS: {
    PRIMARY: '#165540',
    PRIMARY_LIGHT: '#b4ceb3',
    PRIMARY_DARK: '#013220',
    SECONDARY: '#3a668c',
    SECONDARY_LIGHT: '#b6ccd7',
    SECONDARY_DARK: '#192839',
    ACCENT: '#e6b751',
    ACCENT_LIGHT: '#f2d084',
    ACCENT_DARK: '#d49e2a',
  },
  BREAKPOINTS: {
    MOBILE: '768px',
    TABLET: '1024px',
    DESKTOP: '1200px',
  },
} as const;

// Error Messages
export const ERROR_MESSAGES = {
  NETWORK_ERROR: 'Network error. Please check your connection and try again.',
  AUTHENTICATION_ERROR: 'Authentication failed. Please log in again.',
  FILE_TOO_LARGE: `File size exceeds ${FILE_CONFIG.MAX_SIZE_MB}MB limit.`,
  INVALID_FILE_TYPE: 'Invalid file type. Please upload PDF, Word, or text files only.',
  UPLOAD_FAILED: 'Failed to upload file. Please try again.',
  CHAT_ERROR: 'Failed to send message. Please try again.',
  SESSION_ERROR: 'Failed to load chat session. Please refresh the page.',
  GENERIC_ERROR: 'Something went wrong. Please try again.',
} as const;

// Success Messages
export const SUCCESS_MESSAGES = {
  FILE_UPLOADED: 'File uploaded successfully!',
  MESSAGE_SENT: 'Message sent successfully!',
  SESSION_CREATED: 'New chat session created!',
  SESSION_DELETED: 'Chat session deleted successfully!',
  DOCUMENT_DELETED: 'Document deleted successfully!',
} as const;

// Feature Flags
export const FEATURES = {
  DOCUMENT_UPLOAD: import.meta.env.VITE_ENABLE_DOCUMENT_UPLOAD === 'true',
  CONVERSATION_SAVE: import.meta.env.VITE_ENABLE_CONVERSATION_SAVE === 'true',
  VOICE_INPUT: import.meta.env.VITE_ENABLE_VOICE_INPUT === 'true',
  DARK_MODE: import.meta.env.VITE_ENABLE_DARK_MODE === 'true',
} as const;

// Real Estate Context
export const REAL_ESTATE_CONTEXT = {
  SYSTEM_PROMPT: `You are RR Realty AI, an expert real estate assistant with deep knowledge of:
- Property analysis and valuation
- Market trends and investment strategies
- Real estate law and regulations
- Contract analysis and negotiation
- Property management and development

Provide professional, accurate, and helpful responses. Always consider legal and financial implications.
If asked about specific legal advice, recommend consulting with a qualified attorney.`,
  
  SUGGESTED_PROMPTS: [
    "What should I look for when analyzing a property investment?",
    "Help me understand this real estate contract",
    "What are the current market trends in my area?",
    "How do I calculate property ROI?",
    "What are the key factors in property valuation?",
  ],
} as const;
