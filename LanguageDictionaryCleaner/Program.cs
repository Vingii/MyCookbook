using Newtonsoft.Json;

namespace LanguageDictionaryCleaner
{
    class Program
    {
        static void Main(string[] args)
        {
            string inputFilePath;
            if (args.Length == 0)
            {
                Console.WriteLine("Input file path:");
                inputFilePath = Console.ReadLine() ?? "";
            }
            else
            {
                inputFilePath = args[0];
            }

            var outputPath = Path.GetDirectoryName(inputFilePath) + "\\" + Path.GetFileNameWithoutExtension(inputFilePath) + "_output" + Path.GetExtension(inputFilePath);

            using var reader = new StreamReader(inputFilePath);
            using var writer = new StreamWriter(outputPath);

            string? line = reader.ReadLine();
            while (line != null)
            {
                try
                {
                    var wordDetails = JsonConvert.DeserializeObject<WordDetails>(line);
                    var wordInflections = WordTransformer.GetInflections(wordDetails ?? new WordDetails());
                    if (wordInflections.Inflections.Count > 0)
                    {
                        writer.WriteLine(JsonConvert.SerializeObject(wordInflections));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing line: {ex}");
                }

                line = reader.ReadLine();
            }
        }
    }
}