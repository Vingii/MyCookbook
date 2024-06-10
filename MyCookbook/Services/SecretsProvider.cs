using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace MyCookbook.Services
{
    public static class SecretsProvider
    {
        public static string GetSecret(this WebApplicationBuilder builder, string key)
        {
            return builder.Environment.IsProduction()
                ? GetAzureSecret(key)
                : builder.Configuration[key];
        }

        private static string GetAzureSecret(string key)
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
