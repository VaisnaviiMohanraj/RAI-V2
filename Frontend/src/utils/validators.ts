import { FILE_CONFIG } from './constants';

// Input validation utilities

/**
 * Validate file upload
 */
export const validateFile = (file: File): { isValid: boolean; error?: string } => {
  // Check file size
  const maxSizeBytes = FILE_CONFIG.MAX_SIZE_MB * 1024 * 1024;
  if (file.size > maxSizeBytes) {
    return {
      isValid: false,
      error: `File size exceeds ${FILE_CONFIG.MAX_SIZE_MB}MB limit`,
    };
  }

  // Check file type
  if (!FILE_CONFIG.ALLOWED_TYPES.includes(file.type as any)) {
    return {
      isValid: false,
      error: 'Invalid file type. Please upload PDF, Word, or text files only',
    };
  }

  // Check file extension as additional validation
  const extension = '.' + file.name.split('.').pop()?.toLowerCase();
  if (!FILE_CONFIG.ALLOWED_EXTENSIONS.includes(extension as any)) {
    return {
      isValid: false,
      error: 'Invalid file extension. Allowed: ' + FILE_CONFIG.ALLOWED_EXTENSIONS.join(', '),
    };
  }

  return { isValid: true };
};

/**
 * Validate email address
 */
export const validateEmail = (email: string): boolean => {
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  return emailRegex.test(email);
};

/**
 * Validate message content
 */
export const validateMessage = (message: string): { isValid: boolean; error?: string } => {
  const trimmed = message.trim();
  
  if (!trimmed) {
    return {
      isValid: false,
      error: 'Message cannot be empty',
    };
  }

  if (trimmed.length > 4000) {
    return {
      isValid: false,
      error: 'Message is too long (maximum 4000 characters)',
    };
  }

  return { isValid: true };
};

/**
 * Validate session title
 */
export const validateSessionTitle = (title: string): { isValid: boolean; error?: string } => {
  const trimmed = title.trim();
  
  if (!trimmed) {
    return {
      isValid: false,
      error: 'Session title cannot be empty',
    };
  }

  if (trimmed.length > 100) {
    return {
      isValid: false,
      error: 'Session title is too long (maximum 100 characters)',
    };
  }

  return { isValid: true };
};

/**
 * Validate URL
 */
export const validateUrl = (url: string): boolean => {
  try {
    new URL(url);
    return true;
  } catch {
    return false;
  }
};

/**
 * Validate phone number (US format)
 */
export const validatePhoneNumber = (phone: string): boolean => {
  const phoneRegex = /^[\+]?[1]?[\s\-\.]?[\(]?[0-9]{3}[\)]?[\s\-\.]?[0-9]{3}[\s\-\.]?[0-9]{4}$/;
  return phoneRegex.test(phone);
};

/**
 * Validate required field
 */
export const validateRequired = (value: string | null | undefined): boolean => {
  return value !== null && value !== undefined && value.toString().trim() !== '';
};

/**
 * Validate string length
 */
export const validateLength = (
  value: string,
  min: number = 0,
  max: number = Infinity
): { isValid: boolean; error?: string } => {
  const length = value.length;
  
  if (length < min) {
    return {
      isValid: false,
      error: `Must be at least ${min} characters long`,
    };
  }
  
  if (length > max) {
    return {
      isValid: false,
      error: `Must be no more than ${max} characters long`,
    };
  }
  
  return { isValid: true };
};

/**
 * Validate numeric value
 */
export const validateNumber = (
  value: string | number,
  min?: number,
  max?: number
): { isValid: boolean; error?: string } => {
  const num = typeof value === 'string' ? parseFloat(value) : value;
  
  if (isNaN(num)) {
    return {
      isValid: false,
      error: 'Must be a valid number',
    };
  }
  
  if (min !== undefined && num < min) {
    return {
      isValid: false,
      error: `Must be at least ${min}`,
    };
  }
  
  if (max !== undefined && num > max) {
    return {
      isValid: false,
      error: `Must be no more than ${max}`,
    };
  }
  
  return { isValid: true };
};

/**
 * Validate password strength
 */
export const validatePassword = (password: string): { 
  isValid: boolean; 
  score: number; 
  feedback: string[] 
} => {
  const feedback: string[] = [];
  let score = 0;
  
  // Length check
  if (password.length >= 8) {
    score += 1;
  } else {
    feedback.push('Use at least 8 characters');
  }
  
  // Uppercase check
  if (/[A-Z]/.test(password)) {
    score += 1;
  } else {
    feedback.push('Include uppercase letters');
  }
  
  // Lowercase check
  if (/[a-z]/.test(password)) {
    score += 1;
  } else {
    feedback.push('Include lowercase letters');
  }
  
  // Number check
  if (/\d/.test(password)) {
    score += 1;
  } else {
    feedback.push('Include numbers');
  }
  
  // Special character check
  if (/[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(password)) {
    score += 1;
  } else {
    feedback.push('Include special characters');
  }
  
  return {
    isValid: score >= 3,
    score,
    feedback,
  };
};
