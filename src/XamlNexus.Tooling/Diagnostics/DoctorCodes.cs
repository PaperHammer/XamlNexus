namespace XamlNexus.Tooling.Diagnostics;

/// <summary>Doctor 对外诊断编号的唯一登记处；保留现有编号以兼容调用方。</summary>
public static class DoctorCodes {
    public const string OperatingSystem = "XD1001";
    public const string DotnetSdk = "XD1002";
    public const string NugetSources = "XD1003";
    public const string Architecture = "XD1004";
    public const string WindowsSdk = "XD1005";
    public const string EnvironmentBoundary = "XD1006";
    public const string ProjectStructure = "XD2001";
    public const string ProjectXml = "XD3001";
    public const string WindowsTargets = "XD3002";
    public const string WindowsAppSdk = "XD3003";
    public const string InstalledRecipes = "XD4001";
    public const string RecipeAvailability = "XD4002";
    public const string RecipeVersionMatch = "XD4003";
    public const string RecipeVersionMismatch = "XD4004";
    public const string DataProjectReference = "XD5001";
    public const string WalInitialization = "XD5002";
    public const string ProtobufIntegration = "XD5003";
    public const string ReleaseModule = "XD6001";
    public const string ReleaseMetadata = "XD6002";
}
