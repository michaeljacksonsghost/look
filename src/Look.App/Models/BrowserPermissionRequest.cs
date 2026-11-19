namespace Look.Models;

/// <summary>A permission prompt surfaced by a browser engine. Permission is denied unless the UI explicitly allows it.</summary>
public sealed class BrowserPermissionRequest : EventArgs
{
    private readonly Action<bool> _respond;

    public BrowserPermissionRequest(string origin, string permissionKind, Action<bool> respond)
    {
        Origin = origin;
        PermissionKind = permissionKind;
        _respond = respond;
    }

    public string Origin { get; }
    public string PermissionKind { get; }
    public void Respond(bool allow) => _respond(allow);
}
