using MyCookbook.Components;
using System.Net.Http.Headers;

namespace MyCookbook.Services
{
    public class CannyFeedbackProvider : IFeedbackProvider
    {
        private string _key;
        private string _url;
        private string _boardId;
        private string _userId;
        private HttpClient Client { get; set; } = new HttpClient();

        public CannyFeedbackProvider(string key, string url, string boardId, string userId) 
        { 
            _key = key;
            _url = url;
            _boardId = boardId;
            _userId = userId;
        }

        public async Task ProvideFeedback(string feedback)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _url)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        { "apiKey", _key },
                        { "authorID", _userId },
                        { "boardID", _boardId },
                        { "title", "Anonymous Feedback" },
                        { "details", feedback },
                    })
            };

            await Client.SendAsync(request);
        }
    }
}
