export interface Message {
  id: string;
  content: string;
  sender: 'user' | 'ai';
  timestamp: Date;
  isUser?: boolean; // Keep for backward compatibility
}

export interface Document {
  id: string;
  fileName: string;
  originalFileName: string;
  contentType: string;
  fileSize: number;
  uploadDate: Date;
  userId: string;
  storagePath?: string;
  extractedText?: string;
}

export interface ChatResponse {
  id?: string;
  content: string;
  message: string; // Alias for content for backward compatibility
  timestamp?: string | Date;
  sessionId?: string;
  isStreaming?: boolean;
}

export interface ChatRequest {
  message: string;
  userId?: string;
  documentIds?: string[];
  conversationId?: string;
}

export interface ChatMessage {
  id: string;
  content: string;
  isUser: boolean;
  timestamp: Date;
  userId: string;
  role: string;
}

export interface FileUploadResponse {
  success: boolean;
  documentId?: string;
  errorMessage?: string;
  fileName?: string;
  fileSize?: number;
}

export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  error?: string;
}

export interface ConversationSession {
  id: string;
  title: string;
  lastMessageTime: string;
  messageCount: number;
  lastMessage: string;
}

export interface ChatSession {
  id: string;
  title: string;
  messages: Message[];
  timestamp: Date;
  messageCount: number;
  userId?: string;
  createdAt?: Date;
  lastMessageAt?: Date;
}

export interface User {
  id: string;
  name: string;
  email: string;
  avatar?: string;
  preferences?: UserPreferences;
}

export interface UserPreferences {
  theme: 'light' | 'dark';
  language: string;
  notifications: boolean;
}
