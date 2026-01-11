using System;
using System.Collections.Generic;

namespace MasterOfPuppets.Extensions;

public static class ListExtensions {
    /// <summary>
    /// Move a list item in safe way
    /// </summary>
    public static void MoveItemToIndex<T>(this IList<T> list, int itemIndex, int targetIndex) {
        if (list == null || list.Count == 0)
            return;

        var isValidIndex = itemIndex >= 0 && itemIndex < list.Count;
        var isValidTargetIndex = targetIndex >= 0 && targetIndex < list.Count;

        if (!isValidIndex || !isValidTargetIndex)
            return;

        targetIndex = Math.Clamp(targetIndex, 0, list.Count - 1);

        var item = list[itemIndex];
        list.RemoveAt(itemIndex);
        list.Insert(targetIndex, item);
    }

    public static bool IndexExists<T>(this IList<T> list, int index) {
        return index >= 0 && index < list.Count;
    }

    /// <summary>
    /// Adds an item only if it does not exist in the list.
    /// </summary>
    public static bool AddUnique<T>(this IList<T> list, T item) {
        if (!list.Contains(item)) {
            list.Add(item);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Adds multiple items only if they do not exist in the list.
    /// </summary>
    public static void AddRangeUnique<T>(this IList<T> list, IEnumerable<T> items) {
        foreach (var item in items) {
            list.AddUnique(item);
        }
    }

}
