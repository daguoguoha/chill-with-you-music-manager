// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace ChillSaltPlayerPlaylist {
    public sealed class FavoriteOperation {
        public bool Liked,Unconfirmed;
        public string Error;
        public static FavoriteOperation Run(Func<bool> read,Action<bool> write,bool? desired) {
            bool before=read();bool failed=false;string error=null;
            if(desired.HasValue && before!=desired.Value){try{write(desired.Value);}catch(Exception ex){failed=true;error=ex.Message;}}
            return new FavoriteOperation{Liked=desired.HasValue?read():before,Unconfirmed=failed,Error=error};
        }
    }
}
