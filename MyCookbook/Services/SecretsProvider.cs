using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace MyCookbook.Services
{
    public class SecretsProvider : ISecretsProvider
    {
        private WebApplicationBuilder Builder { get; }

        public SecretsProvider(WebApplicationBuilder builder)
        {
            Builder = builder;
        }

        public string GetSecret(string key)
        {
            return Builder.Environment.IsProduction()
                ? GetAzureSecret(key)
                : Builder.Configuration[key];
        }

        private string GetAzureSecret(string key)
        {
            var options = new SecretClientOptions()
            {
                Retry =
                {
                    Delay = TimeSpan.FromSeconds(2),
                    MaxDelay = TimeSpan.FromSeconds(16),
                    MaxRetries = 3,
                    Mode = RetryMode.Exponential
                }
            };
            var client = new SecretClient(new Uri("https://mycookbookpdnvault.vault.azure.net/"), new DefaultAzureCredential(), options);

            KeyVaultSecret secret = client.GetSecret(key.Replace(":", "-"));

            return secret.Value;
        }
    }
}
