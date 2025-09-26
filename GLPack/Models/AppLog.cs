namespace GLPack.Models
{
    public class AppLog
    {
        public long Id { get; set; }
        public DateTime TsUtc { get; set; }

        public int? CompanyId { get; set; }

        public string SourceFile { get; set; } = "";
        public string SourceFunction { get; set; } = "";

        public string EventType { get; set; } = "";
        public string Level { get; set; } = "";
        public string LogCode { get; set; } = "";
        public string LogMessage { get; set; } = "";
    }
}
