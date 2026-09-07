using System;

using Lua;

using MasterOfPuppets.LuaScripting.Runtime;

namespace MasterOfPuppets.LuaScripting.Runs;

public sealed record LuaRunError(
    string Category,
    string Message,
    string? ChunkName,
    int? Line,
    int? Column,
    string StackTrace) {

    public static LuaRunError FromException(Exception exception) {
        ArgumentNullException.ThrowIfNull(exception);
        var root = Unwrap(exception);
        return root switch {
            LuaScriptValidationException validation => new LuaRunError(
                "compile",
                validation.Result.Message,
                validation.Result.ChunkName,
                validation.Result.Line,
                validation.Result.Column,
                exception.ToString()),
            LuaQuotaExceededException quota => new LuaRunError(
                $"quota:{quota.Quota}",
                quota.Message,
                null,
                null,
                null,
                exception.ToString()),
            LuaCompileException compile => new LuaRunError(
                "compile",
                compile.MainMessage,
                compile.ChunkName,
                compile.Position.Line,
                compile.Position.Column,
                exception.ToString()),
            LuaRuntimeException runtime => new LuaRunError(
                "runtime",
                runtime.Message,
                null,
                null,
                null,
                exception.ToString()),
            _ => new LuaRunError(
                "host",
                root.Message,
                null,
                null,
                null,
                exception.ToString()),
        };
    }

    private static Exception Unwrap(Exception exception) {
        while (exception.InnerException != null
            && exception is AggregateException or LuaRuntimeException)
            exception = exception.InnerException;
        return exception;
    }
}
