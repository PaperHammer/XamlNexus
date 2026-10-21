namespace XamlNexus.Common.Recipes;

/// <summary>比较 Recipe 和工具使用的语义版本，支持数字核心版本及点分预发布标识。</summary>
public static class XamlNexusRecipeVersion {
    public static int Compare(string left, string right) {
        ArgumentException.ThrowIfNullOrWhiteSpace(left);
        ArgumentException.ThrowIfNullOrWhiteSpace(right);

        (Version leftVersion, string? leftPrerelease) = Parse(left);
        (Version rightVersion, string? rightPrerelease) = Parse(right);
        int coreComparison = leftVersion.CompareTo(rightVersion);
        if (coreComparison != 0) return coreComparison;
        if (leftPrerelease is null) return rightPrerelease is null ? 0 : 1;
        if (rightPrerelease is null) return -1;

        string[] leftParts = leftPrerelease.Split('.');
        string[] rightParts = rightPrerelease.Split('.');
        for (int index = 0; index < Math.Max(leftParts.Length, rightParts.Length); index++) {
            if (index >= leftParts.Length) return -1;
            if (index >= rightParts.Length) return 1;
            bool leftNumeric = leftParts[index].All(char.IsDigit);
            bool rightNumeric = rightParts[index].All(char.IsDigit);
            int comparison = leftNumeric && rightNumeric
                ? CompareNumeric(leftParts[index], rightParts[index])
                : leftNumeric
                    ? -1
                    : rightNumeric
                        ? 1
                        : string.CompareOrdinal(leftParts[index], rightParts[index]);
            if (comparison != 0) return comparison;
        }
        return 0;
    }

    public static bool TryCompare(string left, string right, out int comparison) {
        try {
            comparison = Compare(left, right);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException) {
            comparison = 0;
            return false;
        }
    }

    private static int CompareNumeric(string left, string right) {
        string normalizedLeft = left.TrimStart('0');
        string normalizedRight = right.TrimStart('0');
        if (normalizedLeft.Length == 0) normalizedLeft = "0";
        if (normalizedRight.Length == 0) normalizedRight = "0";
        int lengthComparison = normalizedLeft.Length.CompareTo(normalizedRight.Length);
        return lengthComparison != 0
            ? lengthComparison
            : string.CompareOrdinal(normalizedLeft, normalizedRight);
    }

    private static (Version Version, string? Prerelease) Parse(string value) {
        string[] parts = value.Split('-', 2);
        return (Version.Parse(parts[0]), parts.Length == 2 ? parts[1] : null);
    }
}
