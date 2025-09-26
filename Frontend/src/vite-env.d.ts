/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_AUTH_CLIENT_ID?: string
  readonly VITE_AUTH_AUTHORITY?: string
  readonly VITE_API_SCOPE?: string
  readonly VITE_FORCE_PRODUCTION_URL?: string
  readonly VITE_API_BASE_URL?: string
  readonly VITE_API_TIMEOUT?: string
  readonly VITE_MAX_FILE_SIZE_MB?: string
  readonly VITE_ENABLE_DOCUMENT_UPLOAD?: string
  readonly VITE_ENABLE_CONVERSATION_SAVE?: string
  readonly VITE_ENABLE_VOICE_INPUT?: string
  readonly VITE_ENABLE_DARK_MODE?: string
  readonly NODE_ENV?: string
  readonly DEV?: boolean
  readonly MODE?: string
  readonly PROD?: boolean
  readonly SSR?: boolean
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
