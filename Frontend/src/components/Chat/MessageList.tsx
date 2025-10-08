import React, { useEffect, useRef } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { User, Bot } from 'lucide-react';
import { Message } from '../../types';
import './MessageList.css';

interface MessageListProps {
  messages: Message[];
  isLoading: boolean;
}

const MessageList: React.FC<MessageListProps> = ({ messages, isLoading }) => {
  const messagesEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  const formatTime = (date: Date) => {
    return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  };

  const formatMessageContent = (content: string) => {
    // Convert markdown-like formatting to HTML
    let formatted = content
      // Convert headers
      .replace(/^### (.*$)/gm, '<h3>$1</h3>')
      .replace(/^## (.*$)/gm, '<h2>$1</h2>')
      .replace(/^# (.*$)/gm, '<h1>$1</h1>')
      // Convert bullet points
      .replace(/^- (.*$)/gm, '<li>$1</li>')
      .replace(/^\* (.*$)/gm, '<li>$1</li>')
      // Convert numbered lists
      .replace(/^\d+\. (.*$)/gm, '<li>$1</li>')
      // Convert bold text
      .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
      // Convert italic text
      .replace(/\*(.*?)\*/g, '<em>$1</em>')
      // Convert code blocks
      .replace(/```([\s\S]*?)```/g, '<pre><code>$1</code></pre>')
      // Convert inline code
      .replace(/`(.*?)`/g, '<code>$1</code>')
      // Convert line breaks
      .replace(/\n/g, '<br>');

    // Wrap consecutive list items in ul tags
    formatted = formatted.replace(/(<li>.*<\/li>)(\s*<br>\s*<li>.*<\/li>)*/g, (match) => {
      return '<ul>' + match.replace(/<br>/g, '') + '</ul>';
    });

    return formatted;
  };

  return (
    <div className="message-list">
      <div className="messages-container">
        <AnimatePresence>
          {messages.map((message, index) => (
            <motion.div
              key={message.id}
              data-message-id={message.id}
              className={`message ${message.isUser ? 'message-user' : 'message-ai'}`}
              initial={{ opacity: 0, y: 20, scale: 0.95 }}
              animate={{ opacity: 1, y: 0, scale: 1 }}
              exit={{ opacity: 0, y: -20, scale: 0.95 }}
              transition={{
                duration: 0.3,
                delay: index * 0.05,
                ease: [0.4, 0, 0.2, 1]
              }}
            >
              <div className="message-avatar">
                {message.isUser ? (
                  <User size={16} />
                ) : (
                  <Bot size={16} />
                )}
              </div>
              <div className="message-content">
                <div className="message-bubble">
                  {message.isUser ? (
                    <p className="message-text">{message.content}</p>
                  ) : (
                    <div 
                      className="message-text formatted-content"
                      dangerouslySetInnerHTML={{ __html: formatMessageContent(message.content) }}
                    />
                  )}
                  <span className="message-time">
                    {formatTime(message.timestamp)}
                  </span>
                </div>
              </div>
            </motion.div>
          ))}
        </AnimatePresence>

        {isLoading && (
          <motion.div
            className="message message-ai"
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.3 }}
          >
            <div className="message-avatar">
              <Bot size={16} />
            </div>
            <div className="message-content">
              <div className="message-bubble loading-message">
                <div className="typing-indicator">
                  <span></span>
                  <span></span>
                  <span></span>
                </div>
              </div>
            </div>
          </motion.div>
        )}

        <div ref={messagesEndRef} />
      </div>
    </div>
  );
};

export default MessageList;
