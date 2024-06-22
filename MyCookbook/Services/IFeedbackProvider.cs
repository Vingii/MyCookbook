namespace MyCookbook.Services
{
    public interface IFeedbackProvider
    {
        Task ProvideFeedback(string feedback);
    }
}
