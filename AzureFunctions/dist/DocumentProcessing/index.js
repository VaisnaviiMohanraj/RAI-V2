"use strict";
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.DocumentProcessing = DocumentProcessing;
const functions_1 = require("@azure/functions");
const storage_blob_1 = require("@azure/storage-blob");
const cosmos_1 = require("@azure/cosmos");
function DocumentProcessing(request, context) {
    return __awaiter(this, void 0, void 0, function* () {
        context.log('DocumentProcessing function triggered');
        try {
            // Read request body
            const processingRequest = yield request.json();
            if (!processingRequest || !processingRequest.blobUrl || !processingRequest.documentId || !processingRequest.fileName || !processingRequest.contentType || !processingRequest.userId) {
                context.error('Invalid document processing request');
                return {
                    status: 400,
                    body: "Invalid request format or missing required fields"
                };
            }
            // Process the document
            const result = yield processDocument(context, processingRequest);
            return {
                status: 200,
                headers: { "Content-Type": "application/json" },
                jsonBody: result
            };
        }
        catch (error) {
            context.error('Error in DocumentProcessing function:', error);
            return {
                status: 500,
                body: `Internal server error: ${error.message}`
            };
        }
    });
}
;
function processDocument(context, request) {
    return __awaiter(this, void 0, void 0, function* () {
        try {
            context.log(`Processing document: ${request.fileName}`);
            // Download document from blob storage
            const blobClient = new storage_blob_1.BlobClient(request.blobUrl);
            const downloadResult = yield blobClient.downloadToBuffer();
            // Extract text based on content type
            const extractedText = yield extractTextFromDocument(context, downloadResult, request.contentType, request.fileName);
            // Create searchable chunks
            const chunks = createDocumentChunks(extractedText, request.documentId);
            // Save to Cosmos DB if connection string is available
            yield saveDocumentMetadata(context, request, extractedText, chunks);
            context.log(`Successfully processed document ${request.fileName}. Extracted ${extractedText.length} characters into ${chunks.length} chunks`);
            return {
                success: true,
                documentId: request.documentId,
                extractedText: extractedText,
                chunks: chunks,
                processedAt: new Date().toISOString()
            };
        }
        catch (error) {
            context.error(`Error processing document ${request.fileName}:`, error);
            return {
                success: false,
                documentId: request.documentId,
                extractedText: '',
                chunks: [],
                errorMessage: error.message,
                processedAt: new Date().toISOString()
            };
        }
    });
}
function extractTextFromDocument(context, documentBuffer, contentType, fileName) {
    return __awaiter(this, void 0, void 0, function* () {
        try {
            switch (contentType.toLowerCase()) {
                case 'application/pdf':
                    return yield extractTextFromPdf(context, documentBuffer);
                case 'application/vnd.openxmlformats-officedocument.wordprocessingml.document':
                    return yield extractTextFromWord(context, documentBuffer);
                case 'text/plain':
                    return documentBuffer.toString('utf-8');
                default:
                    context.warn(`Unsupported content type: ${contentType} for file: ${fileName}`);
                    return `Unsupported document type: ${contentType}`;
            }
        }
        catch (error) {
            context.error(`Error extracting text from ${fileName}:`, error);
            return `Error extracting text: ${error.message}`;
        }
    });
}
function extractTextFromPdf(context, pdfBuffer) {
    return __awaiter(this, void 0, void 0, function* () {
        // For now, return a placeholder. In a real implementation, you'd use a PDF parsing library
        // like pdf-parse or call an external service
        context.warn('PDF text extraction not fully implemented - using placeholder');
        return `PDF content placeholder - ${pdfBuffer.length} bytes`;
    });
}
function extractTextFromWord(context, wordBuffer) {
    return __awaiter(this, void 0, void 0, function* () {
        // For now, return a placeholder. In a real implementation, you'd use a library
        // like mammoth or call an external service
        context.warn('Word document text extraction not fully implemented - using placeholder');
        return `Word document content placeholder - ${wordBuffer.length} bytes`;
    });
}
function createDocumentChunks(text, documentId) {
    const chunks = [];
    const chunkSize = 1000; // Characters per chunk
    const overlap = 200; // Overlap between chunks
    if (!text || text.trim().length === 0) {
        return chunks;
    }
    for (let i = 0; i < text.length; i += chunkSize - overlap) {
        const chunkText = text.substring(i, Math.min(i + chunkSize, text.length));
        chunks.push({
            id: generateGuid(),
            documentId: documentId,
            content: chunkText.trim(),
            chunkIndex: chunks.length,
            startPosition: i,
            endPosition: i + chunkText.length
        });
        // Break if we've reached the end
        if (i + chunkSize >= text.length) {
            break;
        }
    }
    return chunks;
}
function saveDocumentMetadata(context, request, extractedText, chunks) {
    return __awaiter(this, void 0, void 0, function* () {
        try {
            const cosmosConnectionString = process.env.COSMOS_DB_CONNECTION_STRING;
            if (!cosmosConnectionString) {
                context.warn('Cosmos DB connection string not configured - skipping metadata save');
                return;
            }
            const cosmosClient = new cosmos_1.CosmosClient(cosmosConnectionString);
            const database = cosmosClient.database("ConversationDB");
            const container = database.container("Documents");
            const documentMetadata = {
                id: request.documentId,
                userId: request.userId,
                fileName: request.fileName,
                contentType: request.contentType,
                extractedTextLength: extractedText.length,
                chunkCount: chunks.length,
                processedAt: new Date().toISOString(),
                blobUrl: request.blobUrl
            };
            yield container.items.upsert(documentMetadata);
            context.log(`Saved document metadata for ${request.fileName}`);
        }
        catch (error) {
            context.error('Error saving document metadata:', error);
            // Don't throw - this is not critical for the main function
        }
    });
}
function generateGuid() {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
        const r = Math.random() * 16 | 0;
        const v = c === 'x' ? r : (r & 0x3 | 0x8);
        return v.toString(16);
    });
}
functions_1.app.http('DocumentProcessing', {
    methods: ['POST'],
    authLevel: 'function',
    handler: DocumentProcessing
});
//# sourceMappingURL=index.js.map