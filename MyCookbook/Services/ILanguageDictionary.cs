namespace MyCookbook.Services
{
    public interface ILanguageDictionary
    {
        IEnumerable<string> WordInflections(string word);
    }
}
