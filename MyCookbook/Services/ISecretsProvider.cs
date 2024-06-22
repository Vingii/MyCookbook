namespace MyCookbook.Services
{
    public interface ISecretsProvider
    {
        public string GetSecret(string key);
    }
}
