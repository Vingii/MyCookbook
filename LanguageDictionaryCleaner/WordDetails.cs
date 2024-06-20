namespace LanguageDictionaryCleaner
{
    public class Form
    {
        public string form { get; set; }
        public string source { get; set; }
        public IList<string> tags { get; set; }
    }

    public class WordDetails
    {
        public IList<Form> forms { get; set; }
        public string word { get; set; }
        public string lang { get; set; }
        public string lang_code { get; set; }
    }
}
