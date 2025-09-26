import axios from 'axios';
import { Document } from '../types';
import { authService } from './authService';

const API_BASE_URL = '/api';

export interface UploadProgress {
  loaded: number;
  total: number;
  percentage: number;
}

export interface FileUploadResponse {
  documentId: string;
  fileName: string;
  fileSize: number;
  success: boolean;
  errorMessage?: string;
}

class DocumentService {
  private async getAuthHeaders(): Promise<Record<string, string>> {
    try {
        if (!authService.isAuthenticated()) {
            console.warn('User not authenticated, skipping token request');
            return {};
        }
        const token = await authService.getAccessToken();
        return token ? { 'Authorization': `Bearer ${token}` } : {};
    } catch (error) {
        console.error('Failed to get access token:', error);
        return {};
    }
  }

  async getDocuments(conversationId?: string): Promise<Document[]> {
    try {
      let url = `${API_BASE_URL}/document`;
      if (conversationId) {
        url += `?conversationId=${conversationId}`;
      }
      const authHeaders = await this.getAuthHeaders();
      const response = await axios.get(url, {
        headers: authHeaders
      });
      return response.data || [];
    } catch (error) {
      console.error('Document service error:', error);
      return [];
    }
  }

  async uploadDocument(
    file: File, 
    conversationId?: string,
    onProgress?: (progress: UploadProgress) => void
  ): Promise<Document> {
    try {
      const formData = new FormData();
      formData.append('file', file);
      formData.append('userId', 'user-1'); // Use consistent user ID
      if (conversationId) {
        formData.append('conversationId', conversationId);
      }

      const authHeaders = await this.getAuthHeaders();
      const response = await axios.post(`${API_BASE_URL}/document/upload`, formData, {
        headers: {
          'Content-Type': 'multipart/form-data',
          ...authHeaders
        },
        onUploadProgress: (progressEvent) => {
          if (onProgress && progressEvent.total) {
            const percentage = Math.round((progressEvent.loaded * 100) / progressEvent.total);
            onProgress({
              loaded: progressEvent.loaded,
              total: progressEvent.total,
              percentage
            });
          }
        },
      });

      if (response.data.success) {
        // Convert FileUploadResponse to Document
        return {
          id: response.data.documentId,
          fileName: response.data.fileName,
          originalFileName: response.data.fileName,
          contentType: file.type,
          fileSize: response.data.fileSize,
          uploadDate: new Date(),
          userId: 'user-1'
        };
      } else {
        throw new Error(response.data.errorMessage || 'Upload failed');
      }
    } catch (error) {
      console.error('Document upload error:', error);
      if (axios.isAxiosError(error)) {
        const message = error.response?.data?.errorMessage || error.message;
        throw new Error(`Upload failed: ${message}`);
      }
      throw new Error('Failed to upload document');
    }
  }

  async deleteDocument(documentId: string): Promise<void> {
    try {
      const authHeaders = await this.getAuthHeaders();
      await axios.delete(`${API_BASE_URL}/document/${documentId}?userId=user-1`, {
        headers: authHeaders
      });
    } catch (error) {
      console.error('Delete document error:', error);
      throw error;
    }
  }
}

export const documentService = new DocumentService();
