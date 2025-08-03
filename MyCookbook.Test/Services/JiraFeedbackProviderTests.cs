using Moq;
using MyCookbook.Services;
using Microsoft.AspNetCore.Components.Forms;
using RichardSzalay.MockHttp;
using System.Net;
using System.Text;
using System.Text.Json;

namespace MyCookbook.Test.Services
{
    public class JiraFeedbackProviderTests
    {
        private readonly MockHttpMessageHandler _mockHttp;
        private readonly HttpClient _httpClient;
        private readonly JiraFeedbackProvider _provider;

        private const string DummyJiraDomain = "my-company.atlassian.net";
        private const string DummyProjectKey = "PROJ";
        private const string DummyEmail = "user@example.com";
        private const string DummyApiToken = "MyApiToken";

        public JiraFeedbackProviderTests()
        {
            _mockHttp = new MockHttpMessageHandler();
            _httpClient = _mockHttp.ToHttpClient();
            _provider = new JiraFeedbackProvider(_httpClient, DummyJiraDomain, DummyEmail, DummyApiToken, DummyProjectKey);
        }

        [Fact]
        public void Constructor_ShouldSetDefaultHeadersCorrectly()
        {
            var expectedAuthToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{DummyEmail}:{DummyApiToken}"));
            Assert.Equal("Basic", _httpClient.DefaultRequestHeaders.Authorization?.Scheme);
            Assert.Equal(expectedAuthToken, _httpClient.DefaultRequestHeaders.Authorization?.Parameter);
            Assert.Contains(_httpClient.DefaultRequestHeaders.Accept, h => h.MediaType == "application/json");
        }

        [Fact]
        public async Task ProvideFeedback_WithTextOnly_ShouldCreateIssueAndNotUploadAttachments()
        {
            // Arrange
            var feedbackText = "This is a test feedback.";
            var expectedIssueId = "10001";
            var createIssueUrl = $"https://{DummyJiraDomain}/rest/api/3/issue";

            _mockHttp.Expect(HttpMethod.Post, createIssueUrl)
                .Respond("application/json", JsonSerializer.Serialize(new { id = expectedIssueId }));

            // Act
            await _provider.ProvideFeedback(feedbackText, null);

            // Assert
            _mockHttp.VerifyNoOutstandingExpectation();
        }

        [Fact]
        public async Task ProvideFeedback_WithTextAndFiles_ShouldCreateIssueAndUploadAttachments()
        {
            // Arrange
            var feedbackText = "Feedback with attachments.";
            var expectedIssueId = "10002";
            var createIssueUrl = $"https://{DummyJiraDomain}/rest/api/3/issue";
            var addAttachmentUrl = $"https://{DummyJiraDomain}/rest/api/3/issue/{expectedIssueId}/attachments";
            var mockFiles = new List<IBrowserFile>
            {
                CreateMockBrowserFile("file1.txt", "text/plain", "content1"),
                CreateMockBrowserFile("file2.jpg", "image/jpeg", "content2")
            };

            _mockHttp.Expect(HttpMethod.Post, createIssueUrl)
                .Respond("application/json", JsonSerializer.Serialize(new { id = expectedIssueId }));

            _mockHttp.Expect(HttpMethod.Post, addAttachmentUrl)
                .WithHeaders("X-Atlassian-Token", "no-check")
                .Respond(HttpStatusCode.OK);

            _mockHttp.Expect(HttpMethod.Post, addAttachmentUrl)
                .WithHeaders("X-Atlassian-Token", "no-check")
                .Respond(HttpStatusCode.OK);

            // Act
            await _provider.ProvideFeedback(feedbackText, mockFiles);

            // Assert
            _mockHttp.VerifyNoOutstandingExpectation();
        }

        [Fact]
        public async Task ProvideFeedback_WhenIssueCreationFails_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var createIssueUrl = $"https://{DummyJiraDomain}/rest/api/3/issue";
            var feedbackText = "This will fail.";

            _mockHttp.When(HttpMethod.Post, createIssueUrl)
                .Respond(HttpStatusCode.BadRequest, "application/json", "{\"errorMessages\":[\"Project not found\"]}");

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _provider.ProvideFeedback(feedbackText, null)
            );
            Assert.Contains("Project not found", exception.Message);
        }

        [Fact]
        public async Task ProvideFeedback_WhenAttachmentUploadFails_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var feedbackText = "Attachment will fail.";
            var expectedIssueId = "10003";
            var createIssueUrl = $"https://{DummyJiraDomain}/rest/api/3/issue";
            var addAttachmentUrl = $"https://{DummyJiraDomain}/rest/api/3/issue/{expectedIssueId}/attachments";
            var mockFiles = new List<IBrowserFile> { CreateMockBrowserFile("fail.zip", "application/zip", "zipcontent") };

            _mockHttp.When(HttpMethod.Post, createIssueUrl)
                .Respond("application/json", JsonSerializer.Serialize(new { id = expectedIssueId }));

            _mockHttp.When(HttpMethod.Post, addAttachmentUrl)
                .Respond(HttpStatusCode.Forbidden, "text/plain", "Attachment upload is not permitted.");

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _provider.ProvideFeedback(feedbackText, mockFiles)
            );
            Assert.Contains("Attachment upload is not permitted.", exception.Message);
        }

        private IBrowserFile CreateMockBrowserFile(string fileName, string contentType, string content)
        {
            var mockFile = new Mock<IBrowserFile>();
            var fileContentBytes = Encoding.UTF8.GetBytes(content);
            var fileStream = new MemoryStream(fileContentBytes);
            mockFile.Setup(f => f.Name).Returns(fileName);
            mockFile.Setup(f => f.ContentType).Returns(contentType);
            mockFile.Setup(f => f.Size).Returns(fileContentBytes.Length);
            mockFile.Setup(f => f.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>()))
                    .Returns(fileStream);
            return mockFile.Object;
        }
    }
}