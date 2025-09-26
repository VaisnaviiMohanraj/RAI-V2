import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { BlobServiceClient } from "@azure/storage-blob";
import { CosmosClient } from "@azure/cosmos";

export async function ConversationPersistence(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
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

    const blobServiceClient = BlobServiceClient.fromConnectionString(connectionString);
    const containerClient = blobServiceClient.getContainerClient(containerName);
    
    // Initialize Cosmos DB client
    const cosmosClient = new CosmosClient(cosmosConnectionString);
    const database = cosmosClient.database("ConversationDB");
    const container = database.container("Conversations");

    try {
        const method = request.method;
        const url = new URL(request.url);
        const userId = url.searchParams.get('userId');
        const sessionId = url.searchParams.get('sessionId');

        switch (method) {
            case 'GET':
                return await handleGetConversation(context, containerClient, container, userId, sessionId);
            case 'POST':
                return await handleSaveConversation(context, request, containerClient, container, userId, sessionId);
            case 'DELETE':
                return await handleDeleteConversation(context, containerClient, container, userId, sessionId);
            default:
                return {
                    status: 405,
                    body: "Method not allowed"
                };
        }
    } catch (error) {
        context.error('Error in conversation persistence:', error);
        return {
            status: 500,
            jsonBody: { error: 'Internal server error' }
        };
    }
};

async function handleGetConversation(context: InvocationContext, containerClient: any, cosmosContainer: any, userId: string | null, sessionId: string | null): Promise<HttpResponseInit> {
    try {
        // First try to get from Cosmos DB for metadata
        const querySpec = {
            query: "SELECT * FROM c WHERE c.userId = @userId AND c.sessionId = @sessionId",
            parameters: [
                { name: "@userId", value: userId },
                { name: "@sessionId", value: sessionId }
            ]
        };

        const { resources: conversations } = await cosmosContainer.items.query(querySpec).fetchAll();
        
        if (conversations.length > 0) {
            // Get detailed conversation from blob storage
            const blobName = `${userId}/${sessionId}.json`;
            const blobClient = containerClient.getBlobClient(blobName);
            
            if (await blobClient.exists()) {
                const downloadResponse = await blobClient.download();
                const content = await streamToString(downloadResponse.readableStreamBody);
                
                return {
                    status: 200,
                    body: JSON.parse(content)
                };
            } else {
                return {
                    status: 200,
                    jsonBody: conversations[0] // Return metadata if blob doesn't exist
                };
            }
        } else {
            return {
                status: 404,
                jsonBody: { error: 'Conversation not found' }
            };
        }
    } catch (error) {
        context.error('Error getting conversation:', error);
        return {
            status: 500,
            jsonBody: { error: 'Failed to retrieve conversation' }
        };
    }
}

async function handleSaveConversation(context: InvocationContext, request: HttpRequest, containerClient: any, cosmosContainer: any, userId: string | null, sessionId: string | null): Promise<HttpResponseInit> {
    try {
        const conversationData = await request.json() as any;
        const blobName = `${userId}/${sessionId}.json`;
        const blobClient = containerClient.getBlockBlobClient(blobName);
        
        // Save detailed conversation to blob storage
        await blobClient.upload(
            JSON.stringify(conversationData), 
            JSON.stringify(conversationData).length,
            {
                blobHTTPHeaders: { blobContentType: 'application/json' }
            }
        );

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

        await cosmosContainer.items.upsert(conversationMetadata);
        
        return {
            status: 200,
            jsonBody: { message: 'Conversation saved successfully' }
        };
    } catch (error) {
        context.error('Error saving conversation:', error);
        return {
            status: 500,
            jsonBody: { error: 'Failed to save conversation' }
        };
    }
}

async function handleDeleteConversation(context: InvocationContext, containerClient: any, cosmosContainer: any, userId: string | null, sessionId: string | null): Promise<HttpResponseInit> {
    try {
        // Delete from blob storage
        const blobName = `${userId}/${sessionId}.json`;
        const blobClient = containerClient.getBlobClient(blobName);
        await blobClient.deleteIfExists();

        // Delete from Cosmos DB
        const itemId = `${userId}_${sessionId}`;
        await cosmosContainer.item(itemId, userId).delete();
        
        return {
            status: 200,
            jsonBody: { message: 'Conversation deleted successfully' }
        };
    } catch (error) {
        context.error('Error deleting conversation:', error);
        return {
            status: 500,
            jsonBody: { error: 'Failed to delete conversation' }
        };
    }
}

async function streamToString(readableStream: any): Promise<string> {
    return new Promise((resolve, reject) => {
        const chunks: any[] = [];
        readableStream.on("data", (data: any) => {
            chunks.push(data.toString());
        });
        readableStream.on("end", () => {
            resolve(chunks.join(""));
        });
        readableStream.on("error", reject);
    });
}

app.http('ConversationPersistence', {
    methods: ['GET', 'POST', 'DELETE'],
    authLevel: 'function',
    handler: ConversationPersistence
});
