import React from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { FileText, Upload } from 'lucide-react';
import DocumentCard from './DocumentCard';
import { Document } from '../../types';
import './DocumentList.css';

interface DocumentListProps {
  documents: Document[];
  onDeleteDocument?: (documentId: string) => void;
  onUploadClick?: () => void;
  compact?: boolean;
  showUploadButton?: boolean;
}

const DocumentList: React.FC<DocumentListProps> = ({
  documents,
  onDeleteDocument,
  onUploadClick,
  compact = false,
  showUploadButton = true
}) => {
  if (documents.length === 0 && !showUploadButton) {
    return null;
  }

  return (
    <div className={`document-list ${compact ? 'compact' : ''}`}>
      {!compact && (
        <div className="document-list-header">
          <div className="header-content">
            <FileText size={18} />
            <h3>Documents ({documents.length})</h3>
          </div>
          {showUploadButton && onUploadClick && (
            <motion.button
              className="upload-button"
              onClick={onUploadClick}
              whileHover={{ scale: 1.05 }}
              whileTap={{ scale: 0.95 }}
            >
              <Upload size={16} />
              <span>Upload</span>
            </motion.button>
          )}
        </div>
      )}

      <div className="documents-container">
        {documents.length === 0 ? (
          <motion.div
            className="empty-state"
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.3 }}
          >
            <FileText className="empty-icon" size={48} />
            <h4>No documents uploaded</h4>
            <p>Upload PDF or DOCX files to analyze them with AI</p>
            {showUploadButton && onUploadClick && (
              <motion.button
                className="upload-button primary"
                onClick={onUploadClick}
                whileHover={{ scale: 1.05 }}
                whileTap={{ scale: 0.95 }}
              >
                <Upload size={16} />
                <span>Upload Document</span>
              </motion.button>
            )}
          </motion.div>
        ) : (
          <AnimatePresence>
            {documents.map((document, index) => (
              <motion.div
                key={document.id}
                initial={{ opacity: 0, y: 20 }}
                animate={{ opacity: 1, y: 0 }}
                exit={{ opacity: 0, y: -20 }}
                transition={{ duration: 0.2, delay: index * 0.05 }}
              >
                <DocumentCard
                  document={document}
                  onDelete={onDeleteDocument}
                  compact={compact}
                />
              </motion.div>
            ))}
          </AnimatePresence>
        )}
      </div>
    </div>
  );
};

export default DocumentList;
