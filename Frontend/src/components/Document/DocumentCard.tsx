import React from 'react';
import { motion } from 'framer-motion';
import { File, FileText, Trash2, Download } from 'lucide-react';
import { Document } from '../../types';
import './DocumentCard.css';

interface DocumentCardProps {
  document: Document;
  onDelete?: (documentId: string) => void;
  compact?: boolean;
}

const DocumentCard: React.FC<DocumentCardProps> = ({ 
  document, 
  onDelete, 
  compact = false 
}) => {
  const formatFileSize = (bytes: number): string => {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  };

  const getFileIcon = (contentType: string) => {
    if (contentType.includes('pdf')) {
      return <File className="file-icon pdf-icon" size={compact ? 16 : 20} />;
    } else if (contentType.includes('word') || contentType.includes('document')) {
      return <FileText className="file-icon doc-icon" size={compact ? 16 : 20} />;
    }
    return <File className="file-icon" size={compact ? 16 : 20} />;
  };

  const formatDate = (dateString: string | Date): string => {
    const date = typeof dateString === 'string' ? new Date(dateString) : dateString;
    return date.toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  };

  const handleDelete = (e: React.MouseEvent) => {
    e.stopPropagation();
    if (onDelete) {
      onDelete(document.id);
    }
  };

  return (
    <motion.div
      className={`document-card ${compact ? 'compact' : ''}`}
      initial={{ opacity: 0, scale: 0.95 }}
      animate={{ opacity: 1, scale: 1 }}
      exit={{ opacity: 0, scale: 0.95 }}
      whileHover={{ scale: compact ? 1.02 : 1.03 }}
      transition={{ duration: 0.2 }}
    >
      <div className="document-header">
        <div className="document-info">
          {getFileIcon(document.contentType)}
          <div className="document-details">
            <h4 className="document-name" title={document.originalFileName}>
              {document.originalFileName}
            </h4>
            {!compact && (
              <div className="document-meta">
                <span className="file-size">{formatFileSize(document.fileSize)}</span>
                <span className="upload-date">{formatDate(document.uploadDate)}</span>
              </div>
            )}
          </div>
        </div>
        
        <div className="document-actions">
          {!compact && (
            <motion.button
              className="action-button download-button"
              whileHover={{ scale: 1.1 }}
              whileTap={{ scale: 0.9 }}
              title="Download document"
            >
              <Download size={14} />
            </motion.button>
          )}
          
          {onDelete && (
            <motion.button
              className="action-button delete-button"
              whileHover={{ scale: 1.1 }}
              whileTap={{ scale: 0.9 }}
              onClick={handleDelete}
              title="Delete document"
            >
              <Trash2 size={14} />
            </motion.button>
          )}
        </div>
      </div>

      {!compact && (
        <div className="document-status">
          <div className="status-indicator processed">
            <div className="status-dot"></div>
            <span>Ready for analysis</span>
          </div>
        </div>
      )}
    </motion.div>
  );
};

export default DocumentCard;
