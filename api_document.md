# API Documentation for Frontend

This document provides the technical details for integrating with the MarkdownGenQAs API.

## Base URL
Default Dev URL: `https://localhost:7128`

---

## 1. Category Management (`/api/CategoryFiles`)

### [GET] Get All Categories
Retrieve a list of all categories and their associated files.
- **URL**: `/api/CategoryFiles`
- **Response**: `200 OK`
```json
[
  {
    "id": "uuid",
    "name": "string",
    "createdAt": "datetime",
    "updatedAt": "datetime",
    "fileMetadatas": [
      {
        "id": "uuid",
        "fileName": "string",
        "fileType": "string",
        "status": "string"
      }
    ]
  }
]
```

### [POST] Create Category
- **URL**: `/api/CategoryFiles`
- **Body**:
```json
{
  "name": "Category Name"
}
```
- **Response**: `201 Created` / `400 Bad Request`

### [PUT] Update Category
- **URL**: `/api/CategoryFiles/{id}`
- **Body**:
```json
{
  "name": "New Category Name"
}
```
- **Response**: `200 OK`

### [DELETE] Delete Category
- **URL**: `/api/CategoryFiles/{id}`
- **Response**: `240 No Content`
- **Note**: Fails if category has associated files.

---

## 2. File Metadata & Upload (`/api/FileMetadatas`)

### [POST] Upload Markdown File
Upload a `.md` file to S3 and create metadata in database.
- **URL**: `/api/FileMetadatas/upload`
- **Content-Type**: `multipart/form-data`
- **Body**:
    - `File`: (Binary) The `.md` file.
    - `CategoryId`: (UUID, Optional)
    - `Author`: (String, Optional)
- **Response**: `201 Created`

### [GET] Get All Files
- **URL**: `/api/FileMetadatas`
- **Response**: `200 OK` (Array of FileMetadata objects)

### [GET] Get File by ID
- **URL**: `/api/FileMetadatas/{id}`
- **Response**: `200 OK`
```json
{
  "id": "uuid",
  "fileName": "string",
  "fileType": "string",
  "status": "string",
  "objectKeyMarkdownOcr": "string",
  "objectKeyDocumentSummary": "string|null",
  "objectKeyChunkQa": "string|null",
  "processingTime": 0,
  "author": "string|null",
  "categoryId": "uuid|null",
  "categoryName": "string|null",
  "createdAt": "datetime",
  "updatedAt": "datetime"
}
```

### [GET] Download File
Download the original `.md` file from S3.
- **URL**: `/api/FileMetadatas/{id}/download`
- **Response**: File stream (`application/octet-stream`)

### [GET] Download QAs Markdown
Generate and download all processed questions and answers as a `.md` file.
- **URL**: `/api/FileMetadatas/{id}/download-qas-markdown`
- **Response**: File stream (`text/markdown`)
  ```markdown
  # Question [number n]: ...
  ## Answer: ...
  ```

### [GET] Download QAs JSON
Download the raw processed questions and answers as a `.json` file.
- **URL**: `/api/FileMetadatas/{id}/download-qas-json`
- **Response**: File stream (`application/json`)
- **Structure**: `List<ChunkQAInfor>`
```json
[
  {
    "chunk_infor": {
      "type": "Text | Table | Summary",
      "tokens_count": 123,
      "title": "Section Title",
      "tittle_hirarchy": "H1 > H2",
      "content": "Raw chunk content..."
    },
    "qas": [
      {
        "question": "What is...?",
        "answer": "It is...",
        "category": "General Knowledge"
      }
    ]
  }
]
```

### [DELETE] Delete File
Permanently delete metadata and all associated S3 files (original, summary, QAs).
- **URL**: `/api/FileMetadatas/{id}`
- **Response**: `204 No Content`

---

## 3. Background Processing & Real-time Updates (`/api/GenQAs`)

### [POST] Trigger QA Generation
Starts the background process for a specific file.
- **URL**: `/api/GenQAs/process/{fileMetadataId}`
- **Response**: `202 Accepted`
```json
{
  "jobId": "hangfire-id"
}
```

### [GET] Real-time Notifications (SSE)
Connect to this endpoint to receive progress updates via Server-Sent Events.
- **URL**: `/api/GenQAs/notifications/{fileMetadataId}`
- **Mechanism**: Server-Sent Events (SSE)
- **Data Format**: `data: {JSON_OBJECT}\n\n`

#### Notification Object Structure:
```json
{
  "fileMetadataId": "uuid",
  "timestamp": "dd/MM/yyyy HH:mm:ss",
  "message": "Status description text",
  "status": "Processing | Successed | Failed | Canceled"
}
```

#### JavaScript Implementation Example:
```javascript
const eventSource = new EventSource(`https://localhost:7128/api/GenQAs/notifications/${fileId}`);

eventSource.onmessage = (event) => {
    const notification = JSON.parse(event.data);
    console.log(`[${notification.timestamp}] ${notification.status}: ${notification.message}`);
    
    // Auto-close logic based on status
    if (notification.status === "Successed" || 
        notification.status === "Failed" || 
        notification.status === "Canceled") {
        console.log("Job Finished. Closing connection.");
        eventSource.close();
    }
};

eventSource.onerror = (err) => {
    console.error("SSE Error:", err);
    eventSource.close();
};
```

### [POST] Cancel Background Job
Request cancellation of a running background job.
- **URL**: `/api/GenQAs/cancel/{fileMetadataId}`
- **Response**: `200 OK`
```json
{
  "message": "Cancellation requested successfully",
  "jobId": "hangfire-id"
}
```

#### Lifecycle & Reconnection:
1. When a job completes (`Successed`), fails (`Failed`), or is cancelled (`Canceled`), the server **closes the connection** after sending the last notification.

---

## 4. Log Messages (`/api/LogMessages`)

### [GET] Get Log by File ID
Retrieve the full log history for a specific file.
- **URL**: `/api/LogMessages/file/{fileMetadataId}`
- **Response**: `200 OK`
```json
{
  "id": "uuid",
  "message": "JSON_STRING_LIST_OF_NOTIFICATIONS",
  "fileMetadataId": "uuid",
  "createdAt": "datetime",
  "updatedAt": "datetime"
}
```
- **Note**: The `message` field is a JSON string. Once parsed, it returns an array of Notification objects (the same structure as SSE notifications).

