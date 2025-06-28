using Microsoft.AspNetCore.Components.Forms;

namespace MyCookbook.Services
{
    public interface IFeedbackProvider
    {
        Task ProvideFeedback(string feedback, IReadOnlyList<IBrowserFile>? files);
    }
}
