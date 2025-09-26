import { useState, useCallback, useEffect } from 'react';
import { Document } from '../types';
import { documentService } from '../services/documentService';

export interface UseDocumentsOptions {
  autoLoad?: boolean;
  onError?: (error: Error) => void;
}

export interface UseDocumentsReturn {
  documents: Document[];
  isLoading: boolean;
  isUploading: boolean;
  error: string | null;
  selectedDocument: Document | null;
  uploadDocument: (file: File) => Promise<Document | null>;
  deleteDocument: (documentId: string) => Promise<void>;
  selectDocument: (document: Document | null) => void;
  refreshDocuments: () => Promise<void>;
  clearError: () => void;
}

/**
 * Custom hook for managing document state and operations
 */
export const useDocuments = (options: UseDocumentsOptions = {}): UseDocumentsReturn => {
  const { autoLoad = true, onError } = options;
  
  const [documents, setDocuments] = useState<Document[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isUploading, setIsUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selectedDocument, setSelectedDocument] = useState<Document | null>(null);

  // Load documents on mount if autoLoad is enabled
  useEffect(() => {
    if (autoLoad) {
      refreshDocuments();
    }
  }, [autoLoad]);

  const refreshDocuments = useCallback(async () => {
    try {
      setIsLoading(true);
      setError(null);
      const userDocuments = await documentService.getDocuments();
      setDocuments(userDocuments);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to load documents';
      setError(errorMessage);
      if (onError) {
        onError(new Error(errorMessage));
      }
    } finally {
      setIsLoading(false);
    }
  }, [onError]);

  const uploadDocument = useCallback(async (file: File): Promise<Document | null> => {
    try {
      setIsUploading(true);
      setError(null);
      
      const uploadedDocument = await documentService.uploadDocument(file);
      
      // Add the new document to the list
      setDocuments(prev => [uploadedDocument, ...prev]);
      
      return uploadedDocument;
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to upload document';
      setError(errorMessage);
      if (onError) {
        onError(new Error(errorMessage));
      }
      return null;
    } finally {
      setIsUploading(false);
    }
  }, [onError]);

  const deleteDocument = useCallback(async (documentId: string) => {
    try {
      setError(null);
      await documentService.deleteDocument(documentId);
      
      // Remove the document from the list
      setDocuments(prev => prev.filter(doc => doc.id !== documentId));
      
      // Clear selection if the deleted document was selected
      if (selectedDocument?.id === documentId) {
        setSelectedDocument(null);
      }
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to delete document';
      setError(errorMessage);
      if (onError) {
        onError(new Error(errorMessage));
      }
    }
  }, [selectedDocument, onError]);

  const selectDocument = useCallback((document: Document | null) => {
    setSelectedDocument(document);
  }, []);

  const clearError = useCallback(() => {
    setError(null);
  }, []);

  return {
    documents,
    isLoading,
    isUploading,
    error,
    selectedDocument,
    uploadDocument,
    deleteDocument,
    selectDocument,
    refreshDocuments,
    clearError,
  };
};
