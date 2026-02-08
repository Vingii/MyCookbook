using Microsoft.AspNetCore.Components.Forms;
using Serilog;
using System.Net.Http.Headers;

namespace MyCookbook.Services
{
    public class YouTrackFeedbackProvider : IFeedbackProvider
    {
        private readonly HttpClient _client;
        private readonly string _baseUrl;
        private readonly string _projectId;
        private readonly string _issueTypeName;

        public YouTrackFeedbackProvider(HttpClient client, string baseUrl, string apiToken, string projectId, string issueTypeName = "Request")
        {
            _client = client;
            _baseUrl = baseUrl.TrimEnd('/');
            _projectId = projectId;
            _issueTypeName = issueTypeName;

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task ProvideFeedback(string feedback, IReadOnlyList<IBrowserFile>? files, string reportingUserName = "")
        {
            string? issueId = await CreateIssue(feedback, reportingUserName);

            if (!string.IsNullOrEmpty(issueId) && files is { Count: > 0 })
            {
                await AddAttachmentsToIssue(issueId, files);
            }
        }

        private async Task<string?> CreateIssue(string feedbackDetails, string reportingUserName = "")
        {
            var url = $"{_baseUrl}/api/issues?fields=id";

            var typeField = new Dictionary<string, object>
            {
                { "name", "Type" },
                { "$type", "SingleEnumIssueCustomField" },
                { "value", new { name = _issueTypeName } }
            };

            var reporterField = new Dictionary<string, object>
            {
                { "name", "Sponsor" },
                { "$type", "SimpleIssueCustomField" },
                { "value", reportingUserName }
            };

            var issueData = new
            {
                summary = "New Feedback Request",
                description = feedbackDetails,
                project = new { shortName = _projectId },
                customFields = new[] { typeField, reporterField }
            };

            try
            {
                var response = await _client.PostAsJsonAsync(url, issueData);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Log.Error($"Error creating YouTrack issue: {response.StatusCode}\n{errorContent}");
                    throw new InvalidOperationException(errorContent);
                }

                var createdIssue = await response.Content.ReadFromJsonAsync<YouTrackIssueResponse>();
                return createdIssue?.Id;
            }
            catch (Exception ex)
            {
                Log.Error($"An exception occurred while creating YouTrack issue: {ex.Message}");
                throw;
            }
        }

        private async Task AddAttachmentsToIssue(string issueId, IReadOnlyList<IBrowserFile> files)
        {
            var url = $"{_baseUrl}/api/issues/{issueId}/attachments?fields=id,name";

            using var content = new MultipartFormDataContent();

            foreach (var file in files)
            {
                var fileStream = file.OpenReadStream(10 * 1024 * 1024);
                var streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);

                content.Add(streamContent, "files", file.Name);
            }

            try
            {
                var response = await _client.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Log.Error($"Failed to upload attachments to YouTrack. Status: {response.StatusCode}\n{errorContent}");
                    throw new InvalidOperationException(errorContent);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"An exception occurred while uploading attachments to YouTrack: {ex.Message}");
                throw;
            }
        }
    }

    public class YouTrackIssueResponse
    {
        public string? Id { get; set; }
        public string? IdReadable { get; set; }
    }
}