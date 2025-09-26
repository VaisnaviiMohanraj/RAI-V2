import React, { useState, useRef, useEffect } from 'react';
import { motion } from 'framer-motion';
import { Send, Paperclip } from 'lucide-react';
import DocumentUpload from '../Document/DocumentUpload';
import './MessageInput.css';

interface MessageInputProps {
  onSendMessage: (message: string) => void;
  onFileUpload?: (file: File) => Promise<void>;
  disabled?: boolean;
}

const MessageInput: React.FC<MessageInputProps> = ({ onSendMessage, onFileUpload, disabled = false }) => {
  const [message, setMessage] = useState('');
  const [isUploadModalOpen, setIsUploadModalOpen] = useState(false);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  useEffect(() => {
    if (textareaRef.current) {
      textareaRef.current.style.height = 'auto';
      textareaRef.current.style.height = `${textareaRef.current.scrollHeight}px`;
    }
  }, [message]);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (message.trim() && !disabled) {
      onSendMessage(message.trim());
      setMessage('');
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSubmit(e);
    }
  };

  const handleAttachmentClick = () => {
    setIsUploadModalOpen(true);
  };

  const handleUploadComplete = async (file: File) => {
    if (onFileUpload) {
      await onFileUpload(file);
    }
    setIsUploadModalOpen(false);
  };

  const handleUploadModalClose = () => {
    setIsUploadModalOpen(false);
  };

  return (
    <div className="message-input-container">
      <motion.form 
        className="message-input-form"
        onSubmit={handleSubmit}
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.3, delay: 0.1 }}
      >
        <div className="input-wrapper">
          <button
            type="button"
            className="attach-button focus-ring"
            aria-label="Attach file"
            disabled={disabled}
            onClick={handleAttachmentClick}
          >
            <Paperclip size={18} />
          </button>
          
          <textarea
            ref={textareaRef}
            value={message}
            onChange={(e) => setMessage(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Type your message..."
            className="message-textarea focus-ring"
            disabled={disabled}
            rows={1}
            maxLength={2000}
          />
          
          <motion.button
            type="submit"
            className={`send-button focus-ring ${message.trim() ? 'send-button-active' : ''}`}
            disabled={!message.trim() || disabled}
            whileHover={{ scale: message.trim() ? 1.05 : 1 }}
            whileTap={{ scale: message.trim() ? 0.95 : 1 }}
            aria-label="Send message"
          >
            <Send size={18} />
          </motion.button>
        </div>
        
        <div className="input-footer">
          <span className="character-count">
            {message.length}/2000
          </span>
          <span className="input-hint">
            Press Enter to send, Shift+Enter for new line
          </span>
        </div>
      </motion.form>
      
      <DocumentUpload
        isOpen={isUploadModalOpen}
        onClose={handleUploadModalClose}
        onUpload={handleUploadComplete}
      />
    </div>
  );
};

export default MessageInput;
