using Microsoft.AspNetCore.Components.Forms;

public class FeedbackResult
{
    public string Text { get; set; } = string.Empty;
    public IReadOnlyList<IBrowserFile>? Files { get; set; }
}