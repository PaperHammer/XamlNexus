namespace XamlNexus.Common.Generators;

/// <summary>生成过程的领域阶段，不携带控制台标记或展示文案。</summary>
public enum GenerationStage { CopyModules, CreateSolution, WriteManifest, ApplyRecipes, ValidateProject, PublishProject }

public sealed record GenerationProgress(GenerationStage Stage, int Completed, int Total, string? Item = null);

/// <summary>仅成功时返回最终目录；失败时保留原异常及其清理诊断。</summary>
public sealed record GenerationResult(string? OutputRoot, Exception? Error) {
    public bool Success => Error is null && OutputRoot is not null;

    public string GetOutputOrThrow() {
        if (Error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(Error).Throw();
        return OutputRoot ?? throw new InvalidOperationException();
    }
}
