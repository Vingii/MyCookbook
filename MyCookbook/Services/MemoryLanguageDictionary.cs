using MyCookbook.Model;
using Newtonsoft.Json;
using System.Collections.ObjectModel;

namespace MyCookbook.Services
{
    public class MemoryLanguageDictionary : ILanguageDictionary
    {
        private Dictionary<string, Collection<string>> Inflections { get; } = new Dictionary<string, Collection<string>>();

        public MemoryLanguageDictionary(string directory)
        { 
            foreach (var file in Directory.GetFiles("wwwroot\\" + directory))
            {
                LoadDictionary(file);
            }
        }

        public IEnumerable<string> WordInflections(string word)
        {
            return Inflections.ContainsKey(word) ? Inflections[word] : new List<string> { word };
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

                    if (!Inflections.ContainsKey(word.Word))
                    {
                        Inflections[word.Word] = new Collection<string>();
                    }

                    foreach (string inflection in word.Inflections.Where(x => !Inflections[word.Word].Contains(x)))
                    {
                        Inflections[word.Word].Add(inflection);
                    }
                }
                catch { }

                line = reader.ReadLine();
            }
        }
    }
}
