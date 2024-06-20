namespace LanguageDictionaryCleaner
{
    internal class WordInflections
    {
        public string? Word { get; set; }
        public IList<string> Inflections { get; set; } = new List<string>();
    }
}
