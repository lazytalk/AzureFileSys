using Microsoft.AspNetCore.SignalR;

namespace FileService.Api.Hubs;

public class UploadProgressHub : Hub
{
    // Client calls this to join a specific upload session (blobPath)
    public Task JoinSession(string blobPath)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, blobPath);
    }

    public Task LeaveSession(string blobPath)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, blobPath);
    }
}
