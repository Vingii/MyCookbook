namespace MyCookbook.Services
{
    public class MemoryLanguageDictionary : ILanguageDictionary
    {
        private Dictionary<string, IEnumerable<string>> Inflections { get; } = new Dictionary<string, IEnumerable<string>>();

        public void LoadDictionary(string language)
        {
            // TODO
        }

        public IEnumerable<string> WordInflections(string word)
        {
            return Inflections.ContainsKey(word) ? Inflections[word] : new List<string> { word };
        }
    }
}
