import axios from 'axios';
import { ChatRequest, ChatResponse, ConversationSession } from '../types';
import { authService } from './authService';

const API_BASE_URL = (import.meta as any).env?.VITE_API_BASE_URL || '/api';

interface ConversationHistoryEntry {
  role: 'user' | 'assistant';
  content: string;
}


class ChatService {
  private conversationHistory: ConversationHistoryEntry[] = [];

  constructor() {
    this.loadConversationFromLocalStorage();
  }

  private async getAuthHeaders(): Promise<Record<string, string>> {
    try {
        if (!authService.isAuthenticated()) {
            console.warn('User not authenticated, skipping token request');
            return {};
        }
        const token = await authService.getAccessToken();
        return token ? { 'Authorization': `Bearer ${token}` } : {};
    } catch (error) {
        console.error('Failed to get access token:', error);
        return {};
    }
  }

  private loadConversationFromLocalStorage(): void {
    try {
      const stored = localStorage.getItem('conversationHistory');
      if (stored) {
        this.conversationHistory = JSON.parse(stored);
      }
    } catch (error) {
      console.error('Failed to load conversation from localStorage:', error);
      this.conversationHistory = [];
    }
  }

  private saveConversationToLocalStorage(): void {
    try {
      localStorage.setItem('conversationHistory', JSON.stringify(this.conversationHistory));
    } catch (error) {
      console.error('Failed to save conversation to localStorage:', error);
    }
  }

  async sendMessage(request: { message: string; sessionId?: string | null; documentContext?: string } | string, conversationId?: string, documentIds?: string[]): Promise<ChatResponse> {
    // Handle both old and new API signatures
    const messageText = typeof request === 'string' ? request : request.message;
    const sessionId = typeof request === 'object' ? request.sessionId : conversationId;
    // Note: documentContext is available for future use but not currently implemented in the API
    try {
      const chatRequest: ChatRequest = {
        message: messageText,
        userId: 'user-1',
        conversationId: sessionId || undefined,
        documentIds
      };

      const authHeaders = await this.getAuthHeaders();
      let response: any;
      try {
        response = await axios.post(`${API_BASE_URL}/chat/message`, chatRequest, {
          timeout: 30000,
          headers: {
            'Content-Type': 'application/json',
            ...authHeaders
          },
          withCredentials: true
        });
      } catch (err: any) {
        response = await axios.post(`${API_BASE_URL}/chat/send`, chatRequest, {
          timeout: 30000, // 30 second timeout
          headers: {
            'Content-Type': 'application/json',
            ...authHeaders
          },
          withCredentials: true
        });
      }

      // Save conversation history as per reference code
      this.conversationHistory.push({ role: 'user', content: messageText });
      this.conversationHistory.push({ role: 'assistant', content: response.data.content });
      
      // Save to localStorage
      this.saveConversationToLocalStorage();

      return response.data;
    } catch (error: any) {
      console.error('Chat service error:', error);
      if (axios.isAxiosError(error)) {
        if (error.response?.status === 500) {
          throw new Error('Server error. Please try again later.');
        } else if (error.code === 'ECONNABORTED') {
          throw new Error('Request timeout. Please try again.');
        } else if (error.response?.status === 404) {
          throw new Error('Chat service not available.');
        }
      }
      throw new Error('Failed to send message. Please check your connection.');
    }
  }

  async createSession(title?: string): Promise<ConversationSession> {
    try {
      const authHeaders = await this.getAuthHeaders();
      const response = await axios.post(`${API_BASE_URL}/chat/sessions`, { title }, {
        timeout: 15000,
        headers: {
          'Content-Type': 'application/json',
          ...authHeaders
        },
        withCredentials: true
      });
      return response.data;
    } catch (error: any) {
      console.error('Create session error:', error);
      throw new Error('Failed to create session');
    }
  }

  async sendStreamingMessage(message: string, documentIds: string[], conversationId: string, onChunk: (chunk: string) => void): Promise<void> {
    try {
      const authHeaders = await this.getAuthHeaders();
      const response = await fetch(`${API_BASE_URL}/chat/stream`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...authHeaders
        },
        credentials: 'include',
        body: JSON.stringify({
          message: message,
          userId: 'user-1',
          documentIds: documentIds,
          conversationId: conversationId
        }),
      });

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      const reader = response.body?.getReader();
      if (!reader) {
        throw new Error('Failed to get response reader');
      }

      const decoder = new TextDecoder();
      let fullResponse = '';
      
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        
        const chunk = decoder.decode(value, { stream: true });
        fullResponse += chunk;
        onChunk(chunk);
      }

      // Save conversation history for streaming
      this.conversationHistory.push({ role: 'user', content: message });
      this.conversationHistory.push({ role: 'assistant', content: fullResponse });
      this.saveConversationToLocalStorage();
    } catch (error) {
      console.error('Streaming chat error:', error);
      throw new Error('Failed to stream message response');
    }
  }

  async getChatHistory(): Promise<ConversationHistoryEntry[]> {
    try {
      const authHeaders = await this.getAuthHeaders();
      const response = await axios.get(`${API_BASE_URL}/chat/history`, {
        headers: authHeaders,
        withCredentials: true
      });
      const backendHistory = response.data || [];
      
      // Sync with backend history if available
      if (backendHistory.length > 0) {
        this.conversationHistory = backendHistory.map((msg: any) => ({
          role: msg.role,
          content: msg.content
        }));
        this.saveConversationToLocalStorage();
      }
      
      return this.conversationHistory;
    } catch (error) {
      console.error('Chat history error:', error);
      // Return localStorage history as fallback
      return this.conversationHistory;
    }
  }

  async clearChatHistory(): Promise<boolean> {
    try {
      const authHeaders = await this.getAuthHeaders();
      const response = await axios.delete(`${API_BASE_URL}/chat/history`, {
        headers: authHeaders,
        withCredentials: true
      });
      
      if (response.status === 200) {
        this.conversationHistory = [];
        this.saveConversationToLocalStorage();
        return true;
      }
      
      return false;
    } catch (error) {
      console.error('Clear chat history error:', error);
      // Clear localStorage as fallback
      this.conversationHistory = [];
      this.saveConversationToLocalStorage();
      return false;
    }
  }

  async getConversationSessions(): Promise<ConversationSession[]> {
    try {
      const authHeaders = await this.getAuthHeaders();
      const response = await axios.get(`${API_BASE_URL}/chat/sessions`, {
        headers: authHeaders,
        withCredentials: true
      });
      return response.data || [];
    } catch (error) {
      console.error('Get conversation sessions error:', error);
      return [];
    }
  }

  async getConversationHistoryBySession(sessionId: string): Promise<ConversationHistoryEntry[]> {
    try {
      const authHeaders = await this.getAuthHeaders();
      // Prefer documented endpoint; fallback to legacy
      let response: any;
      try {
        response = await axios.get(`${API_BASE_URL}/chat/sessions/${sessionId}/messages`, {
          headers: authHeaders,
          withCredentials: true
        });
      } catch (err: any) {
        response = await axios.get(`${API_BASE_URL}/chat/history/${sessionId}`, {
          headers: authHeaders,
          withCredentials: true
        });
      }
      const backendHistory = response.data || [];
      
      return backendHistory.map((msg: any) => ({
        role: msg.role,
        content: msg.content
      }));
    } catch (error: any) {
      console.error('Get conversation history by session error:', error);
      return [];
    }
  }

  async deleteConversationSession(sessionId: string): Promise<boolean> {
    try {
      const authHeaders = await this.getAuthHeaders();
      const response = await axios.delete(`${API_BASE_URL}/chat/sessions/${sessionId}`, {
        headers: authHeaders,
        withCredentials: true
      });
      return response.status === 200;
    } catch (error) {
      console.error('Delete conversation session error:', error);
      return false;
    }
  }

  async getSessionMessages(sessionId: string): Promise<any[]> {
    try {
      const history = await this.getConversationHistoryBySession(sessionId);
      return history.map((entry, index) => ({
        id: `${sessionId}-${index}`,
        content: entry.content,
        sender: entry.role === 'user' ? 'user' : 'ai',
        timestamp: new Date(),
        isUser: entry.role === 'user'
      }));
    } catch (error) {
      console.error('Get session messages error:', error);
      return [];
    }
  }

  getLocalConversationHistory(): ConversationHistoryEntry[] {
    return this.conversationHistory;
  }
}

export const chatService = new ChatService();
