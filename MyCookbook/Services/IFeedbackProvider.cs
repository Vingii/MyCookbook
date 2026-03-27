namespace MyCookbook.Services
{
    public interface IFeedbackProvider
    {
        Task ProvideFeedback(string feedback, IReadOnlyList<IFormFile>? files = null, string reportingUserName = "");
    }
}
