// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace ChillNeteasePlaylist
{
    internal static class PlaylistFocusPolicy
    {
        internal static int Find<T>(IList<T> rows, Func<T,string> identity, string playing)
        {
            if (string.IsNullOrEmpty(playing)) return -1;
            for (int i=0; i<rows.Count; i++) if (string.Equals(identity(rows[i]),playing,StringComparison.Ordinal)) return i;
            return -1;
        }
        internal static bool CanFocus(int request,int current,string expected,string playing,bool destroyed,bool visible)
        { return request==current && !destroyed && visible && !string.IsNullOrEmpty(expected) && string.Equals(expected,playing,StringComparison.Ordinal); }
    }
}
