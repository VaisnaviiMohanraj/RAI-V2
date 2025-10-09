import { useState, useEffect } from 'react';
import { useIsAuthenticated } from '@azure/msal-react';
import AuthWrapper from './components/Auth/AuthWrapper';
import ChatInterface from './components/Chat/ChatInterface';
import Sidebar from './components/UI/Sidebar';
import { documentService } from './services/documentService';
import { chatService } from './services/chatService';
import { StorageManager } from './utils/storageManager';
import type { Message, Document as AppDocument } from './types';
import './App.css';

interface ChatSession {
  id: string;
  title: string;
  messages: Message[];
  timestamp: Date;
  messageCount: number;
}

function App() {
  const isAuthenticated = useIsAuthenticated();
  const [messages, setMessages] = useState<Message[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [chatSessions, setChatSessions] = useState<ChatSession[]>([]);
  const [currentChatId, setCurrentChatId] = useState<string | null>(null);
  const [documents, setDocuments] = useState<AppDocument[]>([]);

  // Load conversation sessions only after authentication
  useEffect(() => {
    if (!isAuthenticated) return;
    
    const loadConversationSessions = async () => {
      try {
        // Check storage availability and quota
        if (!StorageManager.isAvailable()) {
          console.warn('localStorage not available (private mode?)');
        } else {
          const quota = await StorageManager.checkQuota();
          if (!quota.available) {
            console.warn('Storage quota low, cleaning up old sessions...');
            StorageManager.clearOldSessions(10);
          }
        }

        // First, try to load from localStorage
        const storedSessions = localStorage.getItem('chatSessions');
        if (storedSessions) {
          const parsedSessions = JSON.parse(storedSessions);
          setChatSessions(parsedSessions.map((s: any) => ({
            ...s,
            timestamp: new Date(s.timestamp)
          })));
          console.log('Loaded sessions from localStorage:', parsedSessions.length);
        }
        
        // Try to fetch from backend (will fail gracefully if Azure Function is down)
        const sessions = await chatService.getConversationSessions();
        if (Array.isArray(sessions) && sessions.length > 0) {
          const convertedSessions: ChatSession[] = sessions.map(session => ({
            id: session.id,
            title: session.title,
            messages: [],
            timestamp: new Date(session.lastMessageTime),
            messageCount: session.messageCount
          }));
          setChatSessions(convertedSessions);
          StorageManager.safeSetItem('chatSessions', JSON.stringify(convertedSessions));
          console.log('Loaded sessions from backend:', sessions.length);
        }
        
        // Try to restore currentChatId from localStorage
        const savedChatId = localStorage.getItem('currentChatId');
        if (savedChatId) {
          setCurrentChatId(savedChatId);
        } else {
          setMessages([]);
          setCurrentChatId(null);
        }
      } catch (error) {
        console.error('Failed to load conversation sessions:', error);
        // Don't clear sessions on error - keep whatever we have from localStorage
      }
    };
    
    loadConversationSessions();
  }, [isAuthenticated]);

  // Load documents on app start or when conversation changes
  const loadDocuments = async (conversationId?: string) => {
    try {
      console.log('Loading documents for conversation:', conversationId);
      const docs = await documentService.getDocuments(conversationId);
      console.log('Loaded documents:', docs.length, docs.map(d => ({ id: d.id, name: d.originalFileName })));
      setDocuments(docs);
      console.log('Documents state updated:', docs);
    } catch (error) {
      console.error('Failed to load documents:', error);
    }
  };

  // Save currentChatId to localStorage and load conversation-specific documents
  useEffect(() => {
    if (currentChatId) {
      localStorage.setItem('currentChatId', currentChatId);
      loadDocuments(currentChatId);
    } else {
      localStorage.removeItem('currentChatId');
      setDocuments([]); // Clear documents when no conversation is active
    }
  }, [currentChatId]);


  const handleSendMessage = async (content: string) => {
    const userMessage: Message = {
      id: Date.now().toString(),
      content,
      isUser: true,
      sender: 'user',
      timestamp: new Date(),
    };

    // Check if this is an existing chat continuation
    // If we have a currentChatId and either have messages OR documents, treat as existing
    const isExistingChat = currentChatId && 
      (messages.length > 0 || documents.length > 0);

    setMessages(prev => [...prev, userMessage]);
    setIsLoading(true);

    try {
      let conversationId = currentChatId;
      
      if (!isExistingChat) {
        // Generate new conversation ID for new chats
        conversationId = `conv_user-1_${Date.now()}`;
        setCurrentChatId(conversationId);
        console.log('Starting new conversation with ID:', conversationId);
      } else {
        console.log('Continuing existing conversation:', conversationId);
      }

      // Include document IDs from uploaded documents
      const documentIds = documents.map(doc => doc.id);
      console.log('Sending message with document IDs:', documentIds);
      console.log('Documents state:', documents);

      // Create placeholder for streaming assistant message
      const assistantMessageId = (Date.now() + 1).toString();
      const assistantMessage: Message = {
        id: assistantMessageId,
        content: '', // Will be updated as chunks arrive
        isUser: false,
        sender: 'ai',
        timestamp: new Date(),
      };
      
      setMessages(prev => [...prev, assistantMessage]);
      setIsLoading(false); // Show streaming message instead of loading

      let fullResponse = '';

      // Use streaming endpoint
      await chatService.sendStreamingMessage(
        content,
        documentIds,
        conversationId || '',
        (chunk: string) => {
          fullResponse += chunk;
          // Update the assistant message content with accumulated chunks
          setMessages(prev => prev.map(msg => 
            msg.id === assistantMessageId 
              ? { ...msg, content: fullResponse }
              : msg
          ));
        }
      );

      // Streaming complete - scroll to the assistant message
      setTimeout(() => {
        const assistantElement = document.querySelector(`[data-message-id="${assistantMessageId}"]`);
        if (assistantElement) {
          assistantElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
      }, 100);

      const updatedMessages = [...messages, userMessage, { ...assistantMessage, content: fullResponse }];

      // Save messages to localStorage for this conversation
      if (conversationId) {
        localStorage.setItem(`chat_${conversationId}`, JSON.stringify(updatedMessages));
        console.log('Saved chat messages to localStorage:', conversationId);
      }

      // Update chat sessions
      if (!isExistingChat) {
        // Create a new local session for the sidebar
        console.log('Creating new local session for conversation');
        const newSession: ChatSession = {
          id: conversationId!,
          title: content.length > 50 ? content.substring(0, 50) + '...' : content,
          messages: [],
          timestamp: new Date(),
          messageCount: 2 // User + assistant
        };
        setChatSessions(prev => {
          const updated = [newSession, ...prev];
          localStorage.setItem('chatSessions', JSON.stringify(updated));
          return updated;
        });
      } else {
        // For existing chats, update locally without backend refresh
        console.log('Updating existing chat locally');
        setChatSessions(prev => {
          const updated = prev.map(session => {
            if (session.id === currentChatId) {
              return {
                ...session,
                messageCount: session.messageCount + 2, // User + assistant messages
                timestamp: new Date()
              };
            }
            return session;
          });
          
          // Move the updated session to the top
          const updatedSession = updated.find(s => s.id === currentChatId);
          const otherSessions = updated.filter(s => s.id !== currentChatId);
          const final = updatedSession ? [updatedSession, ...otherSessions] : updated;
          localStorage.setItem('chatSessions', JSON.stringify(final));
          return final;
        });
      }
    } catch (error) {
      console.error('Failed to send message:', error);
      setMessages(prev => prev.slice(0, -2)); // Remove both user and empty assistant message on error
      alert('Failed to send message. Please try again.');
    } finally {
      setIsLoading(false);
    }
  };

  const handleNewChat = () => {
    setMessages([]);
    setCurrentChatId(null);
    setDocuments([]); // Clear documents when starting new chat
  };

  const handleSelectChat = async (chatId: string) => {
    try {
      setIsLoading(true);
      
      // Find the session from local state first (since we store in localStorage)
      const localSession = chatSessions.find(session => session.id === chatId);
      
      if (!localSession) {
        console.warn('Session not found in local state:', chatId);
        return;
      }
      
      // Try to load history from backend
      try {
        const history = await chatService.getConversationHistoryBySession(chatId);
        
        if (history && history.length > 0) {
          const convertedMessages: Message[] = history.map((entry, index) => ({
            id: `${chatId}-${index}`,
            content: entry.content,
            isUser: entry.role === 'user',
            sender: entry.role === 'user' ? 'user' : 'ai',
            timestamp: new Date()
          }));
          
          setMessages(convertedMessages);
          setCurrentChatId(chatId);
          console.log('Loaded chat history from backend:', convertedMessages.length, 'messages');
          return;
        }
      } catch (error) {
        console.warn('Backend history unavailable, using localStorage:', error);
      }
      
      // Fallback: Try to load from localStorage
      const savedMessages = localStorage.getItem(`chat_${chatId}`);
      if (savedMessages) {
        const parsedMessages = JSON.parse(savedMessages);
        setMessages(parsedMessages.map((m: any) => ({
          ...m,
          timestamp: new Date(m.timestamp)
        })));
        setCurrentChatId(chatId);
        console.log('Loaded chat history from localStorage:', parsedMessages.length, 'messages');
      } else {
        // If no history found anywhere, at least switch to this chat
        setMessages([]);
        setCurrentChatId(chatId);
        console.log('No history found for chat, starting fresh');
      }
    } catch (error) {
      console.error('Failed to load chat session:', error);
    } finally {
      setIsLoading(false);
    }
  };

  const handleDeleteChat = async (sessionId: string) => {
    try {
      // Try to delete from backend (best effort)
      try {
        await chatService.deleteConversationSession(sessionId);
      } catch (backendError) {
        console.warn('Backend delete failed, continuing with local delete:', backendError);
      }
      
      // Remove from local state and localStorage
      setChatSessions(prev => {
        const updated = prev.filter(session => session.id !== sessionId);
        localStorage.setItem('chatSessions', JSON.stringify(updated));
        return updated;
      });
      
      // If this was the current chat, clear it
      if (currentChatId === sessionId) {
        setMessages([]);
        setCurrentChatId(null);
        localStorage.removeItem('currentChatId');
      }
    } catch (error) {
      console.error('Failed to delete conversation:', error);
    }
  };

  const handleFileUpload = async (file: File) => {
    try {
      // If no active conversation, create one first
      let conversationId = currentChatId;
      if (!conversationId) {
        conversationId = `conv_user-1_${Date.now()}`;
        setCurrentChatId(conversationId);
        console.log('Created new conversation for document upload:', conversationId);
        
        // Add the new conversation to the chat sessions
        const newSession: ChatSession = {
          id: conversationId,
          title: `Chat ${chatSessions.length + 1}`,
          messages: [],
          timestamp: new Date(),
          messageCount: 0
        };
        setChatSessions(prev => [newSession, ...prev]);
      }
      
      const response = await documentService.uploadDocument(file, conversationId);
      console.log('Document uploaded successfully:', response);
      
      // Force a small delay to ensure backend has processed the upload
      await new Promise(resolve => setTimeout(resolve, 100));
      
      // Reload documents for the current conversation
      await loadDocuments(conversationId);
      
      console.log('Document upload and reload completed');
    } catch (error) {
      console.error('Failed to upload document:', error);
      throw error; // Re-throw to let the upload component handle the error display
    }
  };

  const handleDeleteDocument = async (documentId: string) => {
    try {
      await documentService.deleteDocument(documentId);
      
      // Reload documents for the current conversation to reflect the deletion
      await loadDocuments(currentChatId || undefined);
    } catch (error) {
      console.error('Failed to delete document:', error);
    }
  };


  return (
    <AuthWrapper>
      <div className="app">
        <Sidebar
          onNewChat={handleNewChat}
          chatSessions={chatSessions}
          onSelectChat={handleSelectChat}
          onDeleteChat={handleDeleteChat}
          currentChatId={currentChatId}
        />
        <main className="main-content">
          <ChatInterface
            messages={messages}
            isLoading={isLoading}
            onSendMessage={handleSendMessage}
            onFileUpload={handleFileUpload}
            documents={documents}
            onDeleteDocument={handleDeleteDocument}
          />
        </main>
      </div>
    </AuthWrapper>
  );
}

export default App;
