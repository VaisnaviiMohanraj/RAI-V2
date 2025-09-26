import { useState, useCallback, useRef, useEffect } from 'react';
import { Message } from '../types';
import { chatService } from '../services/chatService';

export interface UseChatOptions {
  sessionId?: string | null;
  onError?: (error: Error) => void;
}

export interface UseChatReturn {
  messages: Message[];
  isLoading: boolean;
  error: string | null;
  sendMessage: (content: string, documentContext?: string) => Promise<void>;
  clearMessages: () => void;
  retryLastMessage: () => Promise<void>;
  currentSessionId: string | null;
}

/**
 * Custom hook for managing chat state and operations
 */
export const useChat = (options: UseChatOptions = {}): UseChatReturn => {
  const { sessionId: initialSessionId, onError } = options;
  
  const [messages, setMessages] = useState<Message[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [currentSessionId, setCurrentSessionId] = useState<string | null>(initialSessionId || null);
  
  const lastMessageRef = useRef<{ content: string; documentContext?: string } | null>(null);

  // Load messages when session changes
  useEffect(() => {
    if (currentSessionId) {
      loadSessionMessages(currentSessionId);
    } else {
      setMessages([]);
    }
  }, [currentSessionId]);

  const loadSessionMessages = async (sessionId: string) => {
    try {
      setIsLoading(true);
      const sessionMessages = await chatService.getSessionMessages(sessionId);
      setMessages(sessionMessages);
      setError(null);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to load messages';
      setError(errorMessage);
      if (onError) {
        onError(new Error(errorMessage));
      }
    } finally {
      setIsLoading(false);
    }
  };

  const sendMessage = useCallback(async (content: string, documentContext?: string) => {
    if (!content.trim() || isLoading) return;

    const messageData = { content: content.trim(), documentContext };
    lastMessageRef.current = messageData;

    setIsLoading(true);
    setError(null);

    // Add user message immediately for better UX
    const userMessage: Message = {
      id: Date.now().toString(),
      content: messageData.content,
      sender: 'user',
      timestamp: new Date(),
    };

    setMessages(prev => [...prev, userMessage]);

    try {
      const response = await chatService.sendMessage({
        message: messageData.content,
        sessionId: currentSessionId,
        documentContext: messageData.documentContext,
      });

      // Add AI response
      const aiMessage: Message = {
        id: (Date.now() + 1).toString(),
        content: response.message,
        sender: 'ai',
        timestamp: response.timestamp ? new Date(response.timestamp) : new Date(),
      };

      setMessages(prev => [...prev, aiMessage]);

      // Update session ID if it changed (new session created)
      if (response.sessionId && response.sessionId !== currentSessionId) {
        setCurrentSessionId(response.sessionId);
      }

    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to send message';
      setError(errorMessage);
      
      // Remove the user message on error
      setMessages(prev => prev.slice(0, -1));
      
      if (onError) {
        onError(new Error(errorMessage));
      }
    } finally {
      setIsLoading(false);
    }
  }, [currentSessionId, isLoading, onError]);

  const retryLastMessage = useCallback(async () => {
    if (!lastMessageRef.current) return;
    
    const { content, documentContext } = lastMessageRef.current;
    await sendMessage(content, documentContext);
  }, [sendMessage]);

  const clearMessages = useCallback(() => {
    setMessages([]);
    setError(null);
    setCurrentSessionId(null);
    lastMessageRef.current = null;
  }, []);

  return {
    messages,
    isLoading,
    error,
    sendMessage,
    clearMessages,
    retryLastMessage,
    currentSessionId,
  };
};
