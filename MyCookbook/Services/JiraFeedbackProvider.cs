using Microsoft.AspNetCore.Components.Forms;
using Serilog;
using System.Net.Http.Headers;
using System.Text;

namespace MyCookbook.Services
{
    public class JiraFeedbackProvider : IFeedbackProvider
    {
        private readonly HttpClient _client;
        private readonly string _jiraDomain;
        private readonly string _projectKey;
        private readonly string _issueTypeName;

        public JiraFeedbackProvider(HttpClient client, string jiraDomain, string email, string apiToken, string projectKey, string issueTypeName = "Request")
        {
            _client = client;
            _jiraDomain = jiraDomain;
            _projectKey = projectKey;
            _issueTypeName = issueTypeName;

            var authToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{apiToken}"));
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task ProvideFeedback(string feedback, IReadOnlyList<IBrowserFile>? files, string reportingUserName = "")
        {
            string? issueId = await CreateIssue(feedback);

            if (files is { Count: > 0 })
            {
                await AddAttachmentsToIssue(issueId, files);
            }
        }

        private async Task<string?> CreateIssue(string feedbackDetails)
        {
            var url = $"https://{_jiraDomain}/rest/api/3/issue";

            var issueData = new
            {
                fields = new
                {
                    project = new { key = _projectKey },
                    summary = "Anonymous Feedback",
                    issuetype = new { name = _issueTypeName },
                    description = new
                    {
                        type = "doc",
                        version = 1,
                        content = new[]
                        {
                            new {
                                type = "paragraph",
                                content = new[]
                                {
                                    new {
                                        type = "text",
                                        text = feedbackDetails
                                    }
                                }
                            }
                        }
                    }
                }
            };

            try
            {
                var response = await _client.PostAsJsonAsync(url, issueData);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Log.Error($"Error creating Jira issue: {response.StatusCode}\n{errorContent}");
                    throw new InvalidOperationException(errorContent);
                }

                var createdIssue = await response.Content.ReadFromJsonAsync<JiraIssueResponse>();
                return createdIssue?.Id;
            }
            catch (Exception ex)
            {
                Log.Error($"An exception occurred while creating Jira issue: {ex.Message}");
                throw;
            }
        }

        private async Task AddAttachmentsToIssue(string issueIdOrKey, IReadOnlyList<IBrowserFile> files)
        {
            var url = $"https://{_jiraDomain}/rest/api/3/issue/{issueIdOrKey}/attachments";

            _client.DefaultRequestHeaders.Add("X-Atlassian-Token", "no-check");

            foreach (var file in files)
            {
                try
                {
                    using var content = new MultipartFormDataContent();
                    // Max file size 10MB
                    using var fileStream = file.OpenReadStream(10 * 1024 * 1024);
                    var streamContent = new StreamContent(fileStream);
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);

                    content.Add(streamContent, "file", file.Name);

                    var response = await _client.PostAsync(url, content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        Log.Error($"Failed to upload '{file.Name}'. Status: {response.StatusCode}\n{errorContent}");
                        throw new InvalidOperationException(errorContent);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"An exception occurred while uploading '{file.Name}': {ex.Message}");
                    throw;
                }
            }

            _client.DefaultRequestHeaders.Remove("X-Atlassian-Token");
        }
    }

    public class JiraIssueResponse
    {
        public string? Id { get; set; }
        public string? Key { get; set; }
        public string? Self { get; set; }
    }
}