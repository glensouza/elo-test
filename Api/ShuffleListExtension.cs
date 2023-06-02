using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Api;

public static class ShuffleListExtension
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Shuffle<T>(this IList<T> list)
    {
        if (list == null) throw new ArgumentNullException(nameof(list));
        int n = list.Count;
        while (n > 1)
        {
            int k = Random.Shared.Next(n--);
            (list[n], list[k]) = (list[k], list[n]);
        }
    }
}
