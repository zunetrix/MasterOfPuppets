using System;

using Lua;

namespace MasterOfPuppets.LuaScripting.Runtime;

public static class LuaScriptValidator {
    public const string DefaultChunkName = "@mop-script";

    public static LuaValidationResult Validate(string source, string chunkName = DefaultChunkName) {
        if (string.IsNullOrWhiteSpace(source))
            return new LuaValidationResult(false, "Lua script source is required.", chunkName, 1, 1, null);
        try {
            using var state = LuaState.Create();
            _ = state.Load(source, chunkName, state.Environment);
            return LuaValidationResult.Success(chunkName);
        } catch (LuaCompileException exception) {
            return new LuaValidationResult(
                false,
                exception.MainMessage,
                chunkName,
                exception.Position.Line,
                exception.Position.Column,
                exception.NearToken);
        }
    }

    public static void ThrowIfInvalid(string source, string chunkName = DefaultChunkName) {
        var result = Validate(source, chunkName);
        if (!result.IsValid)
            throw new LuaScriptValidationException(result);
    }
}

public sealed record LuaValidationResult(
    bool IsValid,
    string Message,
    string ChunkName,
    int Line,
    int Column,
    string? NearToken) {

    public static LuaValidationResult Success(string chunkName) => new(true, string.Empty, chunkName, 0, 0, null);

    public string DisplayMessage => IsValid
        ? "Lua syntax is valid."
        : $"{ChunkName}:{Line}:{Column}: {Message}"
            + (string.IsNullOrWhiteSpace(NearToken) ? string.Empty : $" near '{NearToken}'");
}

public sealed class LuaScriptValidationException : ArgumentException {
    public LuaScriptValidationException(LuaValidationResult result)
        : base(result.DisplayMessage, "source") {
        Result = result;
    }

    public LuaValidationResult Result { get; }
}
