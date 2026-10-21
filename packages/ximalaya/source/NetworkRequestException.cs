// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace ChillXimalayaPlaylist
{
    internal sealed class NetworkRequestException : InvalidOperationException
    {
        internal readonly bool CanRetry;
        internal NetworkRequestException(string safeMessage,bool canRetry):base(safeMessage){CanRetry=canRetry;}
    }
}
