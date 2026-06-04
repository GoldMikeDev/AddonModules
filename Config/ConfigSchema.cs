namespace Rename.AddonModules.Config
{
    //"""
    //ToolName/Header:  //(On first entry in file)//
    //  Sub-Header:     //(For sub-menus in editor)//
    //      // Name of option and short explanation (Default: value) [List, of, all, available, choices]
    //      Option = "value"
    //"""
    internal class Schema
    {
        internal static List<string> GetSchema() => [
            """
            SteeleTerm:
                SSH:
                    // Host Key Verification. (Default: TrustAfterFirstUse) [TrustAfterFirstUse, RejectAll, AllowAll]
                    HostKeyVerificationMethod = "TrustAfterFirstUse"
            """,
            """
            
            """
        ];
    }
}