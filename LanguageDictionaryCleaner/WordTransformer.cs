namespace LanguageDictionaryCleaner
{
    internal static class WordTransformer
    {
        private static string[] DisallowedForms = new string[]
        {
            "inanimate", "no-table-tags", "cs-ndecl", "irregular", "cs-adecl", "i-stem", "animate"
        };

        public static WordInflections GetInflections(WordDetails word)
        {
            return new WordInflections()
            {
                Word = word.word,
                Inflections = word.forms
                ?.Where(x => ("declension".Equals(x.source) || (x.tags != null && x.tags.Contains("plural"))) && !DisallowedForms.Contains(x.form))
                .Select(x => x.form)
                .ToList() 
                ?? new List<string>() { word.word }
            };
        }
    }
}
