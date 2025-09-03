namespace MyCookbook.Model
{
    public class ChangelogEntry
    {
        public string Version { get; set; }
        public DateTime ReleaseDate { get; set; }
        public Dictionary<string, List<string>> Changes { get; set; } = new Dictionary<string, List<string>>();
        public string RawHtml { get; set; }
    }
}
