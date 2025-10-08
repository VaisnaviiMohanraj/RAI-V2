import React, { useState, useRef, useCallback } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Upload, X, File, AlertCircle, CheckCircle } from 'lucide-react';
import './DocumentUpload.css';

interface DocumentUploadProps {
  isOpen: boolean;
  onClose: () => void;
  onUpload: (file: File) => Promise<void>;
}

interface UploadState {
  isDragOver: boolean;
  isUploading: boolean;
  uploadProgress: number;
  error: string | null;
  success: boolean;
}

const DocumentUpload: React.FC<DocumentUploadProps> = ({ isOpen, onClose, onUpload }) => {
  const [state, setState] = useState<UploadState>({
    isDragOver: false,
    isUploading: false,
    uploadProgress: 0,
    error: null,
    success: false
  });
  
  const fileInputRef = useRef<HTMLInputElement>(null);

  const resetState = () => {
    setState({
      isDragOver: false,
      isUploading: false,
      uploadProgress: 0,
      error: null,
      success: false
    });
  };

  const handleClose = () => {
    resetState();
    onClose();
  };

  const validateFile = (file: File): string | null => {
    const allowedMimeTypes = [
      'application/pdf',
      'application/vnd.openxmlformats-officedocument.wordprocessingml.document'
    ];
    const allowedExtensions = ['.pdf', '.docx'];
    const fileName = file.name?.toLowerCase() ?? '';
    const hasAllowedExtension = allowedExtensions.some(ext => fileName.endsWith(ext));
    const hasAllowedMimeType = file.type ? allowedMimeTypes.includes(file.type) : false;

    if (!(hasAllowedMimeType || hasAllowedExtension)) {
      return 'Only PDF and DOCX files are supported';
    }

    if (file.size > 10 * 1024 * 1024) { // 10MB limit
      return 'File size must be less than 10MB';
    }

    return null;
  };

  const handleFileUpload = useCallback(async (file: File) => {
    const validationError = validateFile(file);
    if (validationError) {
      setState(prev => ({ ...prev, error: validationError }));
      return;
    }

    setState(prev => ({ 
      ...prev, 
      isUploading: true, 
      error: null, 
      uploadProgress: 0 
    }));

    try {
      // Simulate upload progress
      const progressInterval = setInterval(() => {
        setState(prev => {
          if (prev.uploadProgress >= 90) {
            clearInterval(progressInterval);
            return prev;
          }
          return { ...prev, uploadProgress: prev.uploadProgress + 10 };
        });
      }, 100);

      await onUpload(file);
      
      clearInterval(progressInterval);
      setState(prev => ({ 
        ...prev, 
        isUploading: false, 
        uploadProgress: 100, 
        success: true 
      }));

      // Auto-close after success
      setTimeout(() => {
        handleClose();
      }, 1500);
    } catch (error) {
      setState(prev => ({ 
        ...prev, 
        isUploading: false, 
        error: error instanceof Error ? error.message : 'Upload failed' 
      }));
    }
  }, [onUpload]);

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setState(prev => ({ ...prev, isDragOver: true }));
  }, []);

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setState(prev => ({ ...prev, isDragOver: false }));
  }, []);

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setState(prev => ({ ...prev, isDragOver: false }));
    
    const files = Array.from(e.dataTransfer.files);
    if (files.length > 0) {
      handleFileUpload(files[0]);
    }
  }, [handleFileUpload]);

  const handleFileSelect = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files;
    if (files && files.length > 0) {
      handleFileUpload(files[0]);
    }
  }, [handleFileUpload]);

  const handleBrowseClick = () => {
    fileInputRef.current?.click();
  };

  if (!isOpen) return null;

  return (
    <AnimatePresence>
      <motion.div
        className="document-upload-overlay"
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
        onClick={handleClose}
      >
        <motion.div
          className="document-upload-modal"
          initial={{ opacity: 0, scale: 0.9, y: 20 }}
          animate={{ opacity: 1, scale: 1, y: 0 }}
          exit={{ opacity: 0, scale: 0.9, y: 20 }}
          onClick={(e) => e.stopPropagation()}
        >
          <div className="modal-header">
            <h3 className="modal-title">Upload Document</h3>
            <button
              className="close-button"
              onClick={handleClose}
              disabled={state.isUploading}
            >
              <X size={20} />
            </button>
          </div>

          <div className="modal-content">
            {state.success ? (
              <motion.div
                className="success-state"
                initial={{ opacity: 0, scale: 0.9 }}
                animate={{ opacity: 1, scale: 1 }}
              >
                <CheckCircle className="success-icon" size={48} />
                <h4>Upload Successful!</h4>
                <p>Your document has been uploaded and is ready for analysis.</p>
              </motion.div>
            ) : (
              <>
                <div
                  className={`upload-area ${state.isDragOver ? 'drag-over' : ''} ${state.isUploading ? 'uploading' : ''}`}
                  onDragOver={handleDragOver}
                  onDragLeave={handleDragLeave}
                  onDrop={handleDrop}
                >
                  {state.isUploading ? (
                    <div className="upload-progress">
                      <div className="progress-circle">
                        <svg className="progress-ring" width="60" height="60">
                          <circle
                            className="progress-ring-circle"
                            stroke="var(--color-primary)"
                            strokeWidth="4"
                            fill="transparent"
                            r="26"
                            cx="30"
                            cy="30"
                            style={{
                              strokeDasharray: `${2 * Math.PI * 26}`,
                              strokeDashoffset: `${2 * Math.PI * 26 * (1 - state.uploadProgress / 100)}`,
                            }}
                          />
                        </svg>
                        <span className="progress-text">{state.uploadProgress}%</span>
                      </div>
                      <p>Uploading document...</p>
                    </div>
                  ) : (
                    <div className="upload-content">
                      <Upload className="upload-icon" size={48} />
                      <h4>Drop your document here</h4>
                      <p>or <button className="browse-button" onClick={handleBrowseClick}>browse files</button></p>
                      <div className="file-info">
                        <File size={16} />
                        <span>Supports PDF and DOCX files up to 10MB</span>
                      </div>
                    </div>
                  )}
                </div>

                {state.error && (
                  <motion.div
                    className="error-message"
                    initial={{ opacity: 0, y: 10 }}
                    animate={{ opacity: 1, y: 0 }}
                  >
                    <AlertCircle size={16} />
                    <span>{state.error}</span>
                  </motion.div>
                )}
              </>
            )}
          </div>

          <input
            ref={fileInputRef}
            type="file"
            accept=".pdf,.docx"
            onChange={handleFileSelect}
            style={{ display: 'none' }}
            disabled={state.isUploading}
          />
        </motion.div>
      </motion.div>
    </AnimatePresence>
  );
};

export default DocumentUpload;
