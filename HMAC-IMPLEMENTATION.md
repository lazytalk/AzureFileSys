# HMAC Signature Authentication Implementation Guide

## Overview

HMAC (Hash-based Message Authentication Code) signature validation has been added to protect API endpoints from unauthorized access. Each request must include a cryptographic signature that proves the client knows the shared secret.

## How It Works

1. **Shared Secret**: A secret key stored in Azure Key Vault (`Security--HmacSharedSecret`)
2. **Request Signature**: Each API call must include:
   - `X-Timestamp`: Unix timestamp (seconds since epoch)
   - `X-Signature`: HMAC-SHA256 signature
   - `X-PowerSchool-User`: User identifier
   - `X-PowerSchool-Role`: User role

3. **Signature Formula**:
   ```
   message = timestamp + method + path + user + role
   signature = HMAC-SHA256(message, secret) encoded as Base64
   ```

4. **Validation**: Server rejects requests if:
   - Signature is missing or invalid
   - Timestamp is older than 5 minutes (prevents replay attacks)
   - Required headers are missing

## Getting the Shared Secret

After deployment, retrieve the secret from Key Vault:

```powershell
# For Staging
az keyvault secret show --vault-name kv-fsvc-stg-kwe --name Security--HmacSharedSecret --query value -o tsv

# For Production  
az keyvault secret show --vault-name kv-fsvc-prd-kwe --name Security--HmacSharedSecret --query value -o tsv
```

## JavaScript Implementation (PowerSchool Plugin)

Add this helper function to your PowerSchool plugin HTML:

```javascript
// HMAC-SHA256 signature generation using Web Crypto API
async function computeHmacSignature(message, secret) {
  const encoder = new TextEncoder();
  const keyData = encoder.encode(secret);
  const messageData = encoder.encode(message);
  
  const key = await crypto.subtle.importKey(
    'raw',
    keyData,
    { name: 'HMAC', hash: 'SHA-256' },
    false,
    ['sign']
  );
  
  const signature = await crypto.subtle.sign('HMAC', key, messageData);
  return btoa(String.fromCharCode(...new Uint8Array(signature)));
}

// Generate auth headers with HMAC signature
async function createAuthHeaders(method, path, user, role, secret) {
  const timestamp = Math.floor(Date.now() / 1000);
  const message = `${timestamp}${method}${path}${user}${role}`;
  const signature = await computeHmacSignature(message, secret);
  
  return {
    'X-PowerSchool-User': user,
    'X-PowerSchool-Role': role,
    'X-Timestamp': timestamp.toString(),
    'X-Signature': signature
  };
}

// Example usage
const HMAC_SECRET = 'YOUR_SECRET_FROM_KEYVAULT'; // Store securely!

async function listFiles() {
  const headers = await createAuthHeaders(
    'GET',
    '/api/files',
    'admin@school.com',
    'admin',
    HMAC_SECRET
  );
  
  const response = await fetch('https://filesvc-stg-app.kaiweneducation.com/api/files', {
    method: 'GET',
    headers: {
      ...headers,
      'Content-Type': 'application/json'
    }
  });
  
  return await response.json();
}
```

## Security Notes

### ✅ DO:
- Store the HMAC secret securely (not in plain text in your code if possible)
- Always use HTTPS
- Validate timestamp on server (prevents replay attacks)
- Rotate secrets periodically

### ❌ DON'T:
- Commit secrets to source control
- Expose secrets in client-side JavaScript (PowerSchool plugins run server-side, so this is acceptable)
- Reuse signatures (timestamp ensures uniqueness)
- Skip HTTPS

## Deployment

1. Deploy or update your environment:
   ```powershell
   .\scripts\deploy.ps1 -Environment Staging -CreateResources
   ```

2. The script auto-generates a secure random HMAC secret and stores it in Key Vault

3. Retrieve the secret and add it to your PowerSchool plugin

4. Update all API calls to include HMAC signatures

## Testing

### With HMAC Disabled (Development)
The API checks for the secret configuration. If `Security:HmacSharedSecret` is empty, HMAC validation is skipped.

### With HMAC Enabled
Set `state.hmacSecret` in `staging-tools.html` to test:

```javascript
const state = {
  // ... other properties
  hmacSecret: 'YOUR_SECRET_HERE', // Get from Key Vault
};
```

Then all API calls will automatically include signatures.

## Troubleshooting

### Error: "Missing X-Signature header"
- HMAC is enabled but request doesn't include signature
- Add signature generation to your client code

### Error: "Invalid signature"
- Wrong secret
- Incorrect message format (timestamp + method + path + user + role)
- Encoding mismatch

### Error: "Request timestamp expired or invalid"
- Client clock is off by more than 5 minutes
- Sync system time with NTP server
- Check timezone (use UTC)

## API Endpoints That Require HMAC

All endpoints under `/api/*` except:
- `/api/health/*` (health checks)
- `/swagger` (API documentation)
- Static files (`.html`, `.css`, `.js`)

## Example: Complete Upload Flow

```javascript
const HMAC_SECRET = 'your-secret';
const BASE_URL = 'https://filesvc-stg-app.kaiweneducation.com';
const user = 'teacher@school.com';
const role = 'teacher';

// 1. Begin Upload
async function beginUpload(file) {
  const headers = await createAuthHeaders('POST', '/api/files/begin-upload', user, role, HMAC_SECRET);
  
  const response = await fetch(`${BASE_URL}/api/files/begin-upload`, {
    method: 'POST',
    headers: {
      ...headers,
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({
      fileName: file.name,
      sizeBytes: file.size,
      contentType: file.type
    })
  });
  
  return await response.json(); // { fileId, uploadUrl }
}

// 2. Upload to Blob (no HMAC needed - uses SAS token)
async function uploadToBlob(file, uploadUrl) {
  await fetch(uploadUrl, {
    method: 'PUT',
    headers: {
      'x-ms-blob-type': 'BlockBlob',
      'x-ms-blob-content-type': file.type
    },
    body: file
  });
}

// 3. Complete Upload
async function completeUpload(fileId) {
  const headers = await createAuthHeaders('POST', '/api/files/complete-upload', user, role, HMAC_SECRET);
  
  await fetch(`${BASE_URL}/api/files/complete-upload`, {
    method: 'POST',
    headers: {
      ...headers,
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({ fileId })
  });
}
```

## Production Checklist

- [ ] Deploy with `-CreateResources` to generate HMAC secret
- [ ] Retrieve secret from Key Vault
- [ ] Add secret to PowerSchool plugin (server-side config)
- [ ] Update all API calls to include HMAC signatures
- [ ] Test upload, list, download, delete operations
- [ ] Verify CORS settings allow PowerSchool domain only
- [ ] Consider IP allowlisting as additional security layer
- [ ] Set up monitoring/alerts for 401 errors
