using Newtonsoft.Json;

namespace LanguageDictionaryCleaner
{
    class Program
    {
        static void Main(string[] args)
        {
            string inputFilePath;

            if (args.Length != 0)
            {
                inputFilePath = args[0];

                if (!File.Exists(inputFilePath))
                {
                    inputFilePath = Directory.GetParent(Environment.CurrentDirectory)?.Parent?.Parent?.FullName + "\\" + inputFilePath;
                }

                DictionaryCleaner.ParseFile(inputFilePath);
                return;
            }

            while (true)
            {
                Console.WriteLine("Input file path:");
                inputFilePath = Console.ReadLine() ?? "";

                if (string.IsNullOrEmpty(inputFilePath))
                {
                    return;
                }

                if (!File.Exists(inputFilePath))
                {
                    inputFilePath = Directory.GetParent(Environment.CurrentDirectory)?.Parent?.Parent?.FullName + "\\" + inputFilePath;
                }

                DictionaryCleaner.ParseFile(inputFilePath);
            }
        }
    }
}