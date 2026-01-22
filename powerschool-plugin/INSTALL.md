# PowerSchool File Service Tools Plugin

**Version:** 1.0.0  
**Compatible with:** PowerSchool SIS 23.06+  
**Publisher:** support@non-support.com

## Overview

This PowerSchool plugin provides access to the **File Service Tools** interface from within PowerSchool's admin console. It enables PowerSchool administrators to upload, manage, download, and delete files via the File Service API, with authentication automatically handled through PowerSchool's LDAP user context.

## Features

- **One-click upload** for single or multiple files
- **Real-time upload progress** with speed metrics
- **File listing** with pagination, sorting by name/size/owner/date
- **Selective file deletion** with confirmation
- **Zip download** of selected files for batch export
- **Pre-built preview modal** for image/PDF/text files
- **PowerSchool-integrated authentication** using LDAP user context
- **Responsive UI** for desktop and mobile admin access

## Installation

### 1. Download the Plugin
Extract the plugin files to your PowerSchool plugins directory:
```
<PowerSchool_Installation>/plugins/FileServiceTools/
```

The plugin structure should be:
```
FileServiceTools/
  plugin.xml
  FileServiceTools.psp
```

### 2. Register the Plugin
1. Log in to PowerSchool as a System Administrator
2. Navigate to **System Admin → System Settings → Plugin Management**
3. Click **Install/Register Plugin**
4. Select the `plugin.xml` file from the FileServiceTools directory
5. PowerSchool will validate and register the plugin

### 3. Configure the Plugin
1. After registration, navigate to **System Admin → File Service Tools** (or the location configured in `plugin.xml`)
2. Set the following configuration properties in the plugin admin page:
   - **API Base URL:** `https://filesvc-stg-app.kaiweneducation.com` (or your File Service API URL)
   - **Default Role:** `admin` (used for all authenticated PowerSchool users accessing this plugin)

3. Verify the configuration by accessing the plugin page—you should see your username and role displayed

### 4. Grant Access (Optional)
By default, all PowerSchool users can access this plugin (as specified by `admin="false"` in `plugin.xml`). To restrict access to specific roles:
1. Modify the `plugin.xml` and set `admin="true"` to restrict to System Administrators only, or
2. Use PowerSchool's permission system to grant access to specific user roles

## Configuration Properties

The plugin uses the following configuration properties (stored in PowerSchool's plugin registry):

| Property | Default Value | Description |
|----------|---------------|-------------|
| `api_base_url` | `https://filesvc-stg-app.kaiweneducation.com` | The base URL of the File Service API |
| `default_role` | `admin` | The role to assign to authenticated PowerSchool users when calling the API |

### Updating Configuration
To change the API URL or role:
1. Navigate to **System Admin → File Service Tools**
2. Update the configuration property in PowerSchool's admin interface
3. The `FileServiceTools.psp` page will automatically use the updated values

## API Integration

The plugin communicates with the File Service API using the following headers, automatically injected by `FileServiceTools.psp`:

```
X-PowerSchool-User: <authenticated LDAP username>
X-PowerSchool-Role: <default_role from plugin config>
```

**Endpoints Used:**
- `POST /api/files/begin-upload` — Initiate a file upload
- `POST /api/files/complete-upload/{fileId}` — Finalize a file upload
- `GET /api/files` — List all files
- `GET /api/files/{fileId}` — Get file metadata and download URL
- `DELETE /api/files/{fileId}` — Delete a file
- `POST /api/files/download-zip` — Create a zip export of selected files

## User Context

When accessing the File Service Tools plugin:
- **Username:** Automatically extracted from PowerSchool's LDAP authentication (shown as "Authenticated as: [username]")
- **Role:** Set to `default_role` from plugin config (typically `admin`)
- **No additional login required** — Uses existing PowerSchool session

## Troubleshooting

### Plugin doesn't appear in admin menu
- Verify `plugin.xml` is valid and located in the correct directory
- Check PowerSchool logs: `<PowerSchool_Installation>/logs/plugin_*.log`
- Restart PowerSchool application server after plugin registration

### "Unable to determine user context" error
- Ensure you're logged into PowerSchool with a valid LDAP account
- Check that your PowerSchool session is active (not timed out)
- Verify PowerSchool's authentication system is configured for LDAP

### API calls fail with 401/403
- Verify the `api_base_url` configuration property is correct
- Check that the File Service API is running and accessible from the PowerSchool server
- Ensure the File Service API accepts requests with `X-PowerSchool-User` and `X-PowerSchool-Role` headers
- Review File Service API logs for authentication errors

### Upload fails with network error
- Verify your PowerSchool server has outbound HTTPS access to the File Service API
- Check firewall/proxy rules between PowerSchool and the API
- Verify the SAS URL returned by `/api/files/begin-upload` is accessible

### UI elements don't load or buttons unresponsive
- Clear browser cache (Ctrl+Shift+Del)
- Check browser console for JavaScript errors (F12 → Console)
- Verify PowerSchool has not disabled external JavaScript execution
- Try a different browser

## Updates & Support

For plugin updates, configuration issues, or questions:
- **Email:** support@non-support.com
- **API Documentation:** See the main File Service API README

## File Descriptions

- **plugin.xml** — PowerSchool plugin manifest defining plugin metadata, page registration, and configuration properties
- **FileServiceTools.psp** — PowerSchool Server Page (JSP-based) that:
  - Extracts authenticated user from PowerSchool session
  - Injects user/role and API URL into the UI
  - Provides upload, download, and delete functionality
  - Handles all API communication with File Service

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2024 | Initial release for PowerSchool SIS 23.06 |

---

**Note:** This plugin requires PowerSchool SIS 23.06 or later with LDAP authentication configured. File Service API must be running and accessible from the PowerSchool server.
