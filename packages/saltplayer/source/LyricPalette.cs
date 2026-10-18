// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Globalization;
namespace ChillSaltPlayerPlaylist
{
    internal static class LyricPalette
    {
        internal static readonly string[] Names = { "白色", "浅紫", "浅蓝", "粉色", "薄荷", "暖黄" };
        internal static readonly string[] Hex = { "#F0EBFA", "#CDB6FF", "#AADFFF", "#FFB7D5", "#ADF2DC", "#FFE0A6" };
        internal static bool TryRgb(string text, out float r, out float g, out float b)
        {
            r = g = b = 1;
            if (text == null || text.Length != 7 || text[0] != '#') return false;
            int rgb;
            if (!int.TryParse(text.Substring(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out rgb)) return false;
            r = ((rgb >> 16) & 255) / 255f; g = ((rgb >> 8) & 255) / 255f; b = (rgb & 255) / 255f;
            return true;
        }
    }
}
