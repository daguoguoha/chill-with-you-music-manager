// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ChillSaltPlayerPlaylist
{
    internal sealed class LyricLine
    {
        internal double Time;
        internal string Text;
        internal int Order;
    }
    internal sealed class LyricDocument
    {
        internal const int MaxCharacters = 1024 * 1024;
        internal readonly List<LyricLine> Lines = new List<LyricLine>();
        internal bool Timed;
        static readonly Regex Tag = new Regex(@"\[(\d{1,5}):([0-5]\d)(?:[\.,](\d{1,3}))?\]", RegexOptions.CultureInvariant);
        static readonly Regex Offset = new Regex(@"\[offset:\s*([+-]?\d{1,8})\s*\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        static readonly Regex Metadata = new Regex(@"^\[(?:ar|al|ti|au|by|re|ve|length|offset):[^\]]*\]\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        static readonly Regex WordTag = new Regex(@"<\d{1,6}:\d{2}(?:\.\d{1,3})?>|<\d{1,9},\d{1,9},\d{1,3}>", RegexOptions.CultureInvariant);

        internal static string Clean(string text)
        {
            if (text == null) return "";
            var clean = new StringBuilder(Math.Min(text.Length, 2048));
            foreach (char c in text)
                if (!char.IsControl(c) || c == '\n' || c == '\t')
                { clean.Append(c == '\t' ? ' ' : c); if (clean.Length >= 2048) break; }
            return WordTag.Replace(clean.ToString(), "").Trim();
        }
        internal static LyricDocument Parse(string raw)
        {
            var document = new LyricDocument();
            if (string.IsNullOrWhiteSpace(raw)) return document;
            if (raw.Length > MaxCharacters) throw new InvalidOperationException("歌词文件过大。");
            raw = raw.TrimStart('\uFEFF').Replace("\r\n", "\n").Replace('\r', '\n');
            int offset = 0;
            foreach (Match match in Offset.Matches(raw))
            { int value; if (int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) offset = Math.Max(-600000, Math.Min(600000, value)); }
            var untimed = new List<string>();
            int order = 0;
            foreach (string source in raw.Split('\n'))
            {
                if (order >= 20000) break;
                string line = source.Trim();
                MatchCollection tags = Tag.Matches(line);
                if (tags.Count == 0)
                { if (line.Length > 0 && !Metadata.IsMatch(line)) untimed.Add(Clean(line)); continue; }
                string text = Clean(Tag.Replace(Offset.Replace(line, ""), ""));
                foreach (Match match in tags)
                {
                    double minutes = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                    double seconds = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                    double fraction = match.Groups[3].Success ? double.Parse("0." + match.Groups[3].Value, CultureInfo.InvariantCulture) : 0;
                    // LRC positive offset means the lyric appears earlier.
                    document.Lines.Add(new LyricLine { Time = minutes * 60 + seconds + fraction - offset / 1000.0, Text = text, Order = order++ });
                    if (order >= 20000) break;
                }
            }
            document.Timed = document.Lines.Count > 0;
            if (!document.Timed)
            {
                foreach (string text in untimed)
                { if (document.Lines.Count >= 20000) break; document.Lines.Add(new LyricLine { Time = 0, Text = text, Order = document.Lines.Count }); }
                return document;
            }
            document.Lines.Sort(delegate(LyricLine a, LyricLine b) { int compare = a.Time.CompareTo(b.Time); return compare != 0 ? compare : a.Order.CompareTo(b.Order); });
            // A translation can share its timestamp with the original line.
            for (int i = document.Lines.Count - 1; i > 0; i--)
            {
                LyricLine previous = document.Lines[i - 1], current = document.Lines[i];
                if (Math.Abs(previous.Time - current.Time) > 0.00001) continue;
                if (!string.IsNullOrEmpty(current.Text) && previous.Text != current.Text)
                    previous.Text = Clean(previous.Text + (previous.Text.Length > 0 ? "\n" : "") + current.Text);
                document.Lines.RemoveAt(i);
            }
            return document;
        }
        internal int IndexAt(double position)
        {
            if (Lines.Count == 0 || double.IsNaN(position) || double.IsInfinity(position)) return -1;
            if (!Timed) return 0;
            int low = 0, high = Lines.Count - 1, found = -1;
            while (low <= high)
            {
                int middle = low + (high - low) / 2;
                if (Lines[middle].Time <= position) { found = middle; low = middle + 1; }
                else high = middle - 1;
            }
            return found;
        }
    }
}
