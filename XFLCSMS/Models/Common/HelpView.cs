using XFLCSMS.Models.Tiers;

namespace XFLCSMS.Models.Common
{
    /// <summary>The "How Xpert CSMS works" page (Views/Shared/Help.cshtml).</summary>
    public class HelpView
    {
        public string Layout { get; set; } = "_MakerLayout";
        public string Controller { get; set; } = "Maker";
        /// <summary>Admin, SupportManager, SupportEngineer, Maker or Viewer.</summary>
        public string Role { get; set; } = "Maker";
        public List<SupportLevel> Levels { get; set; } = new();
    }
}
