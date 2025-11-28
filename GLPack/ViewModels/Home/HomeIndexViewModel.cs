namespace GLPack.ViewModels.Home
{
    public sealed class HomeIndexViewModel
    {
        public string? Search { get; set; }
        public List<CompanyQuickPick> QuickPicks { get; set; } = new();
    }

    public sealed class CompanyQuickPick
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }
}
