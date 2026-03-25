using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace MasterOfPuppets.Extensions;

public static class EqualsExtrations {

    /// <summary>
    /// Checks whether an object equals any of the specified objects
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="obj"></param>
    /// <param name="values"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsAny<T>(this T obj, params T[] values) {
        return values.Any(x => x.Equals(obj));
    }

    /// <summary>
    /// Checks whether an object equals any of the specified objects
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="obj"></param>
    /// <param name="values"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsAny<T>(this T obj, IEnumerable<T> values) {
        return values.Any(x => x.Equals(obj));
    }
}
