import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { BlobServiceClient, BlobClient } from "@azure/storage-blob";
import { CosmosClient } from "@azure/cosmos";
import axios from "axios";

export async function DocumentProcessing(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
    context.log('DocumentProcessing function triggered');

    try {
        // Read request body
        const processingRequest = await request.json() as DocumentProcessingRequest;

        if (!processingRequest || !processingRequest.blobUrl || !processingRequest.documentId || !processingRequest.fileName || !processingRequest.contentType || !processingRequest.userId) {
            context.error('Invalid document processing request');
            return {
                status: 400,
                body: "Invalid request format or missing required fields"
            };
        }

        // Process the document
        const result = await processDocument(context, processingRequest);

        return {
            status: 200,
            headers: { "Content-Type": "application/json" },
            jsonBody: result
        };
    } catch (error) {
        context.error('Error in DocumentProcessing function:', error);
        return {
            status: 500,
            body: `Internal server error: ${error.message}`
        };
    }
};

async function processDocument(context: InvocationContext, request: DocumentProcessingRequest): Promise<DocumentProcessingResult> {
    try {
        context.log(`Processing document: ${request.fileName}`);

        // Download document from blob storage
        const blobClient = new BlobClient(request.blobUrl);
        const downloadResult = await blobClient.downloadToBuffer();

        // Extract text based on content type
        const extractedText = await extractTextFromDocument(context, downloadResult, request.contentType, request.fileName);

        // Create searchable chunks
        const chunks = createDocumentChunks(extractedText, request.documentId);

        // Save to Cosmos DB if connection string is available
        await saveDocumentMetadata(context, request, extractedText, chunks);

        context.log(`Successfully processed document ${request.fileName}. Extracted ${extractedText.length} characters into ${chunks.length} chunks`);

        return {
            success: true,
            documentId: request.documentId,
            extractedText: extractedText,
            chunks: chunks,
            processedAt: new Date().toISOString()
        };
    } catch (error) {
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
}

async function extractTextFromDocument(context: InvocationContext, documentBuffer: Buffer, contentType: string, fileName: string): Promise<string> {
    try {
        switch (contentType.toLowerCase()) {
            case 'application/pdf':
                return await extractTextFromPdf(context, documentBuffer);
            
            case 'application/vnd.openxmlformats-officedocument.wordprocessingml.document':
                return await extractTextFromWord(context, documentBuffer);
            
            case 'text/plain':
                return documentBuffer.toString('utf-8');
            
            default:
                context.warn(`Unsupported content type: ${contentType} for file: ${fileName}`);
                return `Unsupported document type: ${contentType}`;
        }
    } catch (error) {
        context.error(`Error extracting text from ${fileName}:`, error);
        return `Error extracting text: ${error.message}`;
    }
}

async function extractTextFromPdf(context: InvocationContext, pdfBuffer: Buffer): Promise<string> {
    // For now, return a placeholder. In a real implementation, you'd use a PDF parsing library
    // like pdf-parse or call an external service
    context.warn('PDF text extraction not fully implemented - using placeholder');
    return `PDF content placeholder - ${pdfBuffer.length} bytes`;
}

async function extractTextFromWord(context: InvocationContext, wordBuffer: Buffer): Promise<string> {
    // For now, return a placeholder. In a real implementation, you'd use a library
    // like mammoth or call an external service
    context.warn('Word document text extraction not fully implemented - using placeholder');
    return `Word document content placeholder - ${wordBuffer.length} bytes`;
}

function createDocumentChunks(text: string, documentId: string): DocumentChunk[] {
    const chunks: DocumentChunk[] = [];
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

async function saveDocumentMetadata(context: InvocationContext, request: DocumentProcessingRequest, extractedText: string, chunks: DocumentChunk[]): Promise<void> {
    try {
        const cosmosConnectionString = process.env.COSMOS_DB_CONNECTION_STRING;
        if (!cosmosConnectionString) {
            context.warn('Cosmos DB connection string not configured - skipping metadata save');
            return;
        }

        const cosmosClient = new CosmosClient(cosmosConnectionString);
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

        await container.items.upsert(documentMetadata);
        context.log(`Saved document metadata for ${request.fileName}`);
    } catch (error) {
        context.error('Error saving document metadata:', error);
        // Don't throw - this is not critical for the main function
    }
}

function generateGuid(): string {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
        const r = Math.random() * 16 | 0;
        const v = c === 'x' ? r : (r & 0x3 | 0x8);
        return v.toString(16);
    });
}

interface DocumentProcessingRequest {
    documentId: string;
    fileName: string;
    contentType: string;
    blobUrl: string;
    userId: string;
}

interface DocumentProcessingResult {
    success: boolean;
    documentId: string;
    extractedText: string;
    chunks: DocumentChunk[];
    errorMessage?: string;
    processedAt: string;
}

interface DocumentChunk {
    id: string;
    documentId: string;
    content: string;
    chunkIndex: number;
    startPosition: number;
    endPosition: number;
}

app.http('DocumentProcessing', {
    methods: ['POST'],
    authLevel: 'function',
    handler: DocumentProcessing
});
