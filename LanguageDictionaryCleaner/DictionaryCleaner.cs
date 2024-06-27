using Newtonsoft.Json;
using System.Linq;

namespace LanguageDictionaryCleaner
{
    internal static class DictionaryCleaner
    {

        public static void ParseFile(string inputFilePath)
        {
            var outputPath = Path.GetDirectoryName(inputFilePath) + "\\" + Path.GetFileNameWithoutExtension(inputFilePath) + "_output" + Path.GetExtension(inputFilePath);

            using var reader = new StreamReader(inputFilePath);

            var baseWordMap = new Dictionary<string, string>();
            var inflectionGroups = new Dictionary<string, List<string>>();

            string? line = reader.ReadLine();
            while (line != null)
            {
                try
                {
                    var wordDetails = JsonConvert.DeserializeObject<WordDetails>(line);
                    var wordInflections = WordTransformer.GetInflections(wordDetails ?? new WordDetails());
                    foreach (var inflection in wordInflections.Inflections)
                    {
                        if (baseWordMap.ContainsKey(inflection)) continue;
                        baseWordMap[inflection] = baseWordMap.ContainsKey(wordInflections.Word ?? "") ? baseWordMap[wordInflections.Word ?? ""] : wordInflections.Word ?? "";
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing line: {ex}");
                }

                line = reader.ReadLine();
            }

            foreach (var (word, baseWord) in baseWordMap)
            {
                if (!inflectionGroups.ContainsKey(baseWord)) inflectionGroups.Add(baseWord, new List<string>());
                inflectionGroups[baseWord].Add(word);
            }

            foreach (var inflectionGroup in inflectionGroups)
            {
                if (!inflectionGroup.Value.Contains(inflectionGroup.Key)) inflectionGroup.Value.Add(inflectionGroup.Key);
            }

            using var writer = new StreamWriter(outputPath);
            foreach (var (word, inflections) in inflectionGroups
                .Where(x => x.Key.Length > 2 && x.Key.Length < 20 && !x.Key.Contains(" "))
                .OrderBy(x => (x.Key.Length, x.Key)))
            {
                writer.WriteLine(JsonConvert.SerializeObject(new WordInflections
                {
                    Word = word,
                    Inflections = inflections
                }));
            }
        }
    }
}
