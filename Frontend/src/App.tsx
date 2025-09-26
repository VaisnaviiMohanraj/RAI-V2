import { useState, useEffect } from 'react';
import { useIsAuthenticated } from '@azure/msal-react';
import AuthWrapper from './components/Auth/AuthWrapper';
import ChatInterface from './components/Chat/ChatInterface';
import Sidebar from './components/UI/Sidebar';
import { documentService } from './services/documentService';
import { chatService } from './services/chatService';
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
        const sessions = await chatService.getConversationSessions();
        // Ensure sessions is an array before calling map
        if (Array.isArray(sessions)) {
          const convertedSessions: ChatSession[] = sessions.map(session => ({
            id: session.id,
            title: session.title,
            messages: [], // Will be loaded when selected
            timestamp: new Date(session.lastMessageTime),
            messageCount: session.messageCount
          }));
          setChatSessions(convertedSessions);
          
          // Try to restore currentChatId from localStorage
          const savedChatId = localStorage.getItem('currentChatId');
          if (savedChatId && sessions.some(s => s.id === savedChatId)) {
            setCurrentChatId(savedChatId);
          } else {
            // On refresh, always start with welcome page (no messages loaded)
            setMessages([]);
            setCurrentChatId(null);
            localStorage.removeItem('currentChatId');
          }
        } else {
          console.warn('Sessions response is not an array:', sessions);
          setChatSessions([]);
        }
      } catch (error) {
        console.error('Failed to load conversation sessions:', error);
        setChatSessions([]);
      }
    };
    
    loadConversationSessions();
    // Don't load documents on app start - they'll be loaded when a conversation is selected
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

      const response = await chatService.sendMessage(content, conversationId || undefined, documentIds);
      
      const assistantMessage: Message = {
        id: (Date.now() + 1).toString(),
        content: response.content,
        isUser: false,
        sender: 'ai',
        timestamp: new Date(),
      };

      setMessages(prev => [...prev, assistantMessage]);

      // Update chat sessions
      if (!isExistingChat) {
        // Only refresh from backend for new conversations
        console.log('Refreshing sessions from backend for new conversation');
        const sessions = await chatService.getConversationSessions();
        if (Array.isArray(sessions)) {
          const convertedSessions: ChatSession[] = sessions.map(session => ({
            id: session.id,
            title: session.title,
            messages: [],
            timestamp: new Date(session.lastMessageTime),
            messageCount: session.messageCount
          }));
          setChatSessions(convertedSessions);
        } else {
          console.warn('Sessions response is not an array during refresh:', sessions);
        }
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
          return updatedSession ? [updatedSession, ...otherSessions] : updated;
        });
      }
    } catch (error) {
      console.error('Failed to send message:', error);
      setMessages(prev => prev.slice(0, -1)); // Remove user message on error
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
      
      // Find the session from backend data to get the real session ID
      const sessions = await chatService.getConversationSessions();
      if (!Array.isArray(sessions)) {
        console.warn('Sessions response is not an array in handleSelectChat:', sessions);
        return;
      }
      const selectedSession = sessions.find(session => session.id === chatId);
      
      if (selectedSession) {
        const history = await chatService.getConversationHistoryBySession(selectedSession.id);
        
        const convertedMessages: Message[] = history.map((entry, index) => ({
          id: `${selectedSession.id}-${index}`,
          content: entry.content,
          isUser: entry.role === 'user',
          sender: entry.role === 'user' ? 'user' : 'ai',
          timestamp: new Date()
        }));
        
        setMessages(convertedMessages);
        setCurrentChatId(selectedSession.id); // Use the actual backend session ID
        // Documents will be loaded automatically by the useEffect when currentChatId changes
      }
    } catch (error) {
      console.error('Failed to load chat session:', error);
    } finally {
      setIsLoading(false);
    }
  };

  const handleDeleteChat = async (sessionId: string) => {
    try {
      await chatService.deleteConversationSession(sessionId);
      
      // Remove from local state
      setChatSessions(prev => prev.filter(session => session.id !== sessionId));
      
      // If this was the current chat, clear it
      if (currentChatId === sessionId) {
        setMessages([]);
        setCurrentChatId(null);
      }
      
      // Refresh sessions from backend to ensure consistency
      const sessions = await chatService.getConversationSessions();
      if (Array.isArray(sessions)) {
        const convertedSessions: ChatSession[] = sessions.map(session => ({
          id: session.id,
          title: session.title,
          messages: [],
          timestamp: new Date(session.lastMessageTime),
          messageCount: session.messageCount
        }));
        setChatSessions(convertedSessions);
      } else {
        console.warn('Sessions response is not an array in handleDeleteChat:', sessions);
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
