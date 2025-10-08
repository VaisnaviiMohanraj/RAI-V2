import React, { useState } from 'react';
import { motion } from 'framer-motion';
import { Trash2, Plus, MessageSquare } from 'lucide-react';
import ConfirmationModal from './Modal';
import './Sidebar.css';

interface ChatSession {
  id: string;
  title: string;
  messages: any[];
  timestamp: Date;
  messageCount: number;
}

interface SidebarProps {
  onNewChat: () => void;
  chatSessions: ChatSession[];
  onSelectChat: (chatId: string) => void;
  onDeleteChat: (chatId: string) => void;
  currentChatId: string | null;
}

const Sidebar: React.FC<SidebarProps> = ({
  onNewChat,
  chatSessions,
  onSelectChat,
  onDeleteChat,
  currentChatId,
}) => {
  const [chatToDelete, setChatToDelete] = useState<{ id: string; title: string } | null>(null);

  const formatDateTime = (date: Date) => {
    const dateStr = date.toLocaleDateString([], { 
      month: 'short', 
      day: 'numeric',
      year: 'numeric'
    });
    const timeStr = date.toLocaleTimeString([], { 
      hour: '2-digit', 
      minute: '2-digit' 
    });
    
    return `${dateStr} • ${timeStr}`;
  };

  return (
    <aside className="sidebar">
      <div className="sidebar-header">
        <h2 className="sidebar-title">R&R Realty AI</h2>
      </div>

      <div className="sidebar-content">
        {/* New Chat Section */}
        <section className="sidebar-section">
          <motion.button
            className="new-chat-button focus-ring"
            onClick={onNewChat}
            whileHover={{ scale: 1.02 }}
            whileTap={{ scale: 0.98 }}
          >
            <Plus size={18} />
            New Chat
          </motion.button>
        </section>


        {/* Chat History Section */}
        <section className="sidebar-section">
          <h3 className="section-title">Recent Chats</h3>
          <div className="chat-history">
            {chatSessions.length === 0 ? (
              <div className="empty-state">
                <MessageSquare size={32} />
                <p>No recent chats</p>
              </div>
            ) : (
              <motion.div className="chat-items">
                {chatSessions.map((session, index) => (
                  <motion.div
                    key={session.id}
                    className={`chat-item ${currentChatId === session.id ? 'active' : ''}`}
                    initial={{ opacity: 0, y: 10 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ delay: index * 0.05 }}
                    whileHover={{ scale: 1.02 }}
                    onClick={() => onSelectChat(session.id)}
                  >
                    <div className="chat-icon">
                      <MessageSquare size={16} />
                    </div>
                    <div className="chat-info">
                      <p className="chat-title" title={session.title}>
                        {session.title}
                      </p>
                      <div className="chat-meta">
                        <span>{session.messageCount} messages • {formatDateTime(session.timestamp)}</span>
                      </div>
                    </div>
                    <button
                      className="chat-delete focus-ring"
                      aria-label={`Delete chat: ${session.title}`}
                      onClick={(e) => {
                        e.stopPropagation();
                        setChatToDelete({ id: session.id, title: session.title });
                      }}
                    >
                      <Trash2 size={14} />
                    </button>
                  </motion.div>
                ))}
              </motion.div>
            )}
          </div>
        </section>
      </div>
      
      <ConfirmationModal
        isOpen={chatToDelete !== null}
        title="Delete Conversation"
        message={`Are you sure you want to delete "${chatToDelete?.title}"? This will permanently delete all messages and documents in this conversation. This action cannot be undone.`}
        confirmText="Delete"
        cancelText="Cancel"
        isDestructive={true}
        onConfirm={() => {
          if (chatToDelete) {
            onDeleteChat(chatToDelete.id);
          }
          setChatToDelete(null);
        }}
        onCancel={() => setChatToDelete(null)}
      />
    </aside>
  );
};

export default Sidebar;
