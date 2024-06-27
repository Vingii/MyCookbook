using MyCookbook.Model;
using Newtonsoft.Json;
using System.Collections.ObjectModel;

namespace MyCookbook.Services
{
    public class MemoryLanguageDictionary : ILanguageDictionary
    {
        private Dictionary<string, List<Collection<string>>> Inflections { get; } = new Dictionary<string, List<Collection<string>>>();

        public MemoryLanguageDictionary(string directory)
        { 
            foreach (var file in Directory.GetFiles("wwwroot\\" + directory))
            {
                LoadDictionary(file);
            }
        }

        public IEnumerable<string> WordInflections(string word)
        {
            word = word.ToLowerInvariant();
            return Inflections.ContainsKey(word) ? Inflections[word].SelectMany(x => x) : new List<string>();
        }

        private void LoadDictionary(string filePath)
        {
            using var reader = new StreamReader(filePath);

            string? line = reader.ReadLine();
            while (line != null)
            {
                try
                {
                    var word = JsonConvert.DeserializeObject<WordInflections>(line);
                    if (word?.Word == null) continue;

                    word.Inflections = word.Inflections.Select(x => x.ToLowerInvariant()).OrderByDescending(x => x.Length).ToList();

                    var collection = new Collection<string>(word.Inflections);

                    foreach (var inflection in word.Inflections)
                    {
                        if (!Inflections.ContainsKey(inflection)) Inflections[inflection] = new List<Collection<string>>();
                        Inflections[inflection].Add(collection);
                    }
                }
                catch { }

                line = reader.ReadLine();
            }
        }
    }
}
