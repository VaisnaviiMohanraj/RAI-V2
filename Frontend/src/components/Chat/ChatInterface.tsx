import React, { useState } from 'react';
import { motion } from 'framer-motion';
import MessageList from './MessageList';
import MessageInput from './MessageInput';
import DocumentUpload from '../Document/DocumentUpload';
import ConfirmationModal from '../UI/Modal';
import { Message, Document } from '../../types';
import { authService } from '../../services/authService';
import './ChatInterface.css';

interface ChatInterfaceProps {
  messages: Message[];
  isLoading: boolean;
  onSendMessage: (message: string) => void;
  onFileUpload?: (file: File) => Promise<void>;
  documents?: Document[];
  onDeleteDocument?: (documentId: string) => void;
}

const ChatInterface: React.FC<ChatInterfaceProps> = ({
  messages,
  isLoading,
  onSendMessage,
  onFileUpload,
  documents = [],
  onDeleteDocument,
}) => {
  const [isUploadModalOpen, setIsUploadModalOpen] = useState(false);
  const [documentToDelete, setDocumentToDelete] = useState<{ id: string; name: string } | null>(null);
  
  // Get user info from auth service
  const userInfo = authService.getUserInfo();
  
  const handleSignOut = async () => {
    try {
      await authService.logout();
    } catch (error) {
      console.error('Sign out error:', error);
    }
  };
  return (
    <div className="chat-interface">
      <motion.header 
        className="chat-header"
        initial={{ opacity: 0, y: -20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.3 }}
      >
        <div className="user-info">
          <span className="user-name">{userInfo?.email || 'User'}</span>
          <button className="signout-button" title="Sign Out" onClick={handleSignOut}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/>
              <polyline points="16,17 21,12 16,7"/>
              <line x1="21" y1="12" x2="9" y2="12"/>
            </svg>
          </button>
        </div>
      </motion.header>

      <div className="chat-content">
        {messages.length === 0 ? (
          <motion.div 
            className="welcome-screen"
            initial={{ opacity: 0, scale: 0.95 }}
            animate={{ opacity: 1, scale: 1 }}
            transition={{ duration: 0.5, delay: 0.2 }}
          >
            <div className="welcome-content">
              <div className="welcome-icon">
                <img 
                  src="/images/RAIlogo.png" 
                  alt="R&R Realty AI Logo" 
                  className="rr-logo-image"
                  width="80"
                  height="80"
                />
              </div>
              <h2 className="welcome-title">Welcome to R&R Realty AI</h2>
              <p className="welcome-description">
                I'm your intelligent assistant, ready to help with any questions or tasks.
                Start a conversation by typing a message below.
              </p>
              <div className="welcome-suggestions">
                <motion.button
                  className="suggestion-chip"
                  whileHover={{ scale: 1.02 }}
                  whileTap={{ scale: 0.98 }}
                  onClick={() => onSendMessage("What can you help me with?")}
                >
                  What can you help me with?
                </motion.button>
                <motion.button
                  className="suggestion-chip"
                  whileHover={{ scale: 1.02 }}
                  whileTap={{ scale: 0.98 }}
                  onClick={() => onSendMessage("Tell me about current market trends")}
                >
                  Current market trends
                </motion.button>
                <motion.button
                  className="suggestion-chip"
                  whileHover={{ scale: 1.02 }}
                  whileTap={{ scale: 0.98 }}
                  onClick={() => onSendMessage("How do I get started?")}
                >
                  How do I get started?
                </motion.button>
              </div>
            </div>
          </motion.div>
        ) : (
          <MessageList messages={messages} isLoading={isLoading} />
        )}
      </div>

      {/* Document Bubbles */}
      {documents.length > 0 && (
        <div className="document-bubbles">
          {documents.map((doc) => (
            <div key={doc.id} className="document-bubble">
              <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="bubble-file-icon">
                <path d="M14.5 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7.5L14.5 2z"></path>
                <polyline points="14 2 14 8 20 8"></polyline>
              </svg>
              <span className="bubble-document-name" title={doc.originalFileName}>
                {doc.originalFileName.length > 30 
                  ? `${doc.originalFileName.substring(0, 30)}...` 
                  : doc.originalFileName
                }
              </span>
              <button 
                className="bubble-delete-button"
                onClick={() => setDocumentToDelete({ id: doc.id, name: doc.originalFileName })}
                title="Remove document"
              >
                <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M18 6 6 18"></path>
                  <path d="m6 6 12 12"></path>
                </svg>
              </button>
            </div>
          ))}
        </div>
      )}

      <MessageInput onSendMessage={onSendMessage} onFileUpload={onFileUpload} disabled={isLoading} />
      
      <DocumentUpload
        isOpen={isUploadModalOpen}
        onClose={() => setIsUploadModalOpen(false)}
        onUpload={async (file) => {
          if (onFileUpload) {
            await onFileUpload(file);
          }
          setIsUploadModalOpen(false);
        }}
      />
      
      <ConfirmationModal
        isOpen={documentToDelete !== null}
        title="Delete Document"
        message={
          <>
            Are you sure you want to delete <span className="document-name">{documentToDelete?.name}</span>? This action cannot be undone.
          </>
        }
        confirmText="Delete"
        cancelText="Cancel"
        isDestructive={true}
        onConfirm={() => {
          if (documentToDelete && onDeleteDocument) {
            onDeleteDocument(documentToDelete.id);
          }
          setDocumentToDelete(null);
        }}
        onCancel={() => setDocumentToDelete(null)}
      />
    </div>
  );
};

export default ChatInterface;
