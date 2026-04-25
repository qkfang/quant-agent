namespace quantweb.Models;

/// <summary>
/// Request to initiate a multi-model debate session.
/// </summary>
public class DebateRequest
{
    public string Query { get; set; } = string.Empty;
    public List<string> SelectedModels { get; set; } = new();
}
