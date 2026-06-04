namespace SqlQueryGenerator.App.Infrastructure;

/// <summary>
/// Centralizes documentation entry points used by the desktop UI.
/// </summary>
public static class ApplicationDocumentation
{
    /// <summary>
    /// Local relative path preferred by the Help button when available.
    /// </summary>
    public const string LocalHelpRelativePath = "docs\\OPERATIONS_GUIDE.md";

    /// <summary>
    /// Remote fallback used when the local documentation file is unavailable.
    /// </summary>
    public const string RemoteHelpUrl = "https://github.com/TristanStark/SqlQueryGenerator/blob/main/docs/OPERATIONS_GUIDE.md";
}
