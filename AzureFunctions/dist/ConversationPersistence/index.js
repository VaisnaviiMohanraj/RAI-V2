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
exports.ConversationPersistence = ConversationPersistence;
const functions_1 = require("@azure/functions");
const storage_blob_1 = require("@azure/storage-blob");
const cosmos_1 = require("@azure/cosmos");
function ConversationPersistence(request, context) {
    return __awaiter(this, void 0, void 0, function* () {
        const connectionString = process.env.AZURE_STORAGE_CONNECTION_STRING;
        const cosmosConnectionString = process.env.COSMOS_DB_CONNECTION_STRING;
        const containerName = "conversations";
        if (!connectionString) {
            return {
                status: 500,
                body: "Storage connection string not configured"
            };
        }
        if (!cosmosConnectionString) {
            return {
                status: 500,
                body: "Cosmos DB connection string not configured"
            };
        }
        const blobServiceClient = storage_blob_1.BlobServiceClient.fromConnectionString(connectionString);
        const containerClient = blobServiceClient.getContainerClient(containerName);
        // Initialize Cosmos DB client
        const cosmosClient = new cosmos_1.CosmosClient(cosmosConnectionString);
        const database = cosmosClient.database("ConversationDB");
        const container = database.container("Conversations");
        try {
            const method = request.method;
            const url = new URL(request.url);
            const userId = url.searchParams.get('userId');
            const sessionId = url.searchParams.get('sessionId');
            switch (method) {
                case 'GET':
                    return yield handleGetConversation(context, containerClient, container, userId, sessionId);
                case 'POST':
                    return yield handleSaveConversation(context, request, containerClient, container, userId, sessionId);
                case 'DELETE':
                    return yield handleDeleteConversation(context, containerClient, container, userId, sessionId);
                default:
                    return {
                        status: 405,
                        body: "Method not allowed"
                    };
            }
        }
        catch (error) {
            context.error('Error in conversation persistence:', error);
            return {
                status: 500,
                jsonBody: { error: 'Internal server error' }
            };
        }
    });
}
;
function handleGetConversation(context, containerClient, cosmosContainer, userId, sessionId) {
    return __awaiter(this, void 0, void 0, function* () {
        try {
            // First try to get from Cosmos DB for metadata
            const querySpec = {
                query: "SELECT * FROM c WHERE c.userId = @userId AND c.sessionId = @sessionId",
                parameters: [
                    { name: "@userId", value: userId },
                    { name: "@sessionId", value: sessionId }
                ]
            };
            const { resources: conversations } = yield cosmosContainer.items.query(querySpec).fetchAll();
            if (conversations.length > 0) {
                // Get detailed conversation from blob storage
                const blobName = `${userId}/${sessionId}.json`;
                const blobClient = containerClient.getBlobClient(blobName);
                if (yield blobClient.exists()) {
                    const downloadResponse = yield blobClient.download();
                    const content = yield streamToString(downloadResponse.readableStreamBody);
                    return {
                        status: 200,
                        body: JSON.parse(content)
                    };
                }
                else {
                    return {
                        status: 200,
                        jsonBody: conversations[0] // Return metadata if blob doesn't exist
                    };
                }
            }
            else {
                return {
                    status: 404,
                    jsonBody: { error: 'Conversation not found' }
                };
            }
        }
        catch (error) {
            context.error('Error getting conversation:', error);
            return {
                status: 500,
                jsonBody: { error: 'Failed to retrieve conversation' }
            };
        }
    });
}
function handleSaveConversation(context, request, containerClient, cosmosContainer, userId, sessionId) {
    return __awaiter(this, void 0, void 0, function* () {
        try {
            const conversationData = yield request.json();
            const blobName = `${userId}/${sessionId}.json`;
            const blobClient = containerClient.getBlockBlobClient(blobName);
            // Save detailed conversation to blob storage
            yield blobClient.upload(JSON.stringify(conversationData), JSON.stringify(conversationData).length, {
                blobHTTPHeaders: { blobContentType: 'application/json' }
            });
            // Save metadata to Cosmos DB
            const conversationMetadata = {
                id: `${userId}_${sessionId}`,
                userId: userId,
                sessionId: sessionId,
                title: conversationData.title || 'New Conversation',
                lastMessageTime: new Date().toISOString(),
                messageCount: conversationData.messages ? conversationData.messages.length : 0,
                lastMessage: conversationData.messages && conversationData.messages.length > 0
                    ? conversationData.messages[conversationData.messages.length - 1].content
                    : '',
                createdAt: conversationData.createdAt || new Date().toISOString(),
                updatedAt: new Date().toISOString()
            };
            yield cosmosContainer.items.upsert(conversationMetadata);
            return {
                status: 200,
                jsonBody: { message: 'Conversation saved successfully' }
            };
        }
        catch (error) {
            context.error('Error saving conversation:', error);
            return {
                status: 500,
                jsonBody: { error: 'Failed to save conversation' }
            };
        }
    });
}
function handleDeleteConversation(context, containerClient, cosmosContainer, userId, sessionId) {
    return __awaiter(this, void 0, void 0, function* () {
        try {
            // Delete from blob storage
            const blobName = `${userId}/${sessionId}.json`;
            const blobClient = containerClient.getBlobClient(blobName);
            yield blobClient.deleteIfExists();
            // Delete from Cosmos DB
            const itemId = `${userId}_${sessionId}`;
            yield cosmosContainer.item(itemId, userId).delete();
            return {
                status: 200,
                jsonBody: { message: 'Conversation deleted successfully' }
            };
        }
        catch (error) {
            context.error('Error deleting conversation:', error);
            return {
                status: 500,
                jsonBody: { error: 'Failed to delete conversation' }
            };
        }
    });
}
function streamToString(readableStream) {
    return __awaiter(this, void 0, void 0, function* () {
        return new Promise((resolve, reject) => {
            const chunks = [];
            readableStream.on("data", (data) => {
                chunks.push(data.toString());
            });
            readableStream.on("end", () => {
                resolve(chunks.join(""));
            });
            readableStream.on("error", reject);
        });
    });
}
functions_1.app.http('ConversationPersistence', {
    methods: ['GET', 'POST', 'DELETE'],
    authLevel: 'function',
    handler: ConversationPersistence
});
//# sourceMappingURL=index.js.map