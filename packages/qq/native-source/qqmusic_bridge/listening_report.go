package main

/*
#include <stdint.h>
*/
import "C"

import (
	"io"
	"net/http"
	"net/url"
	"strconv"
	"time"
	"qqmusic_bridge/api"
)

// Report genuine elapsed playback using the same endpoint as QQ's public web player.
// HTTP success is a transport acknowledgement, not proof of account-duration credit.
//export QQMusicReportPlayback
func QQMusicReportPlayback(id C.longlong, seconds C.longlong, startedAt C.longlong) C.int {
	mu.RLock()
	c := client
	mu.RUnlock()
	if c == nil || !c.IsLoggedIn() { return 401 }
	if id <= 0 || seconds < 1 || seconds > 86400 || startedAt <= 0 { return 400 }
	query := listeningQuery(int64(id),int64(seconds),int64(startedAt),c.GetUIN(),c.GetGUID())
	request, err := http.NewRequest("GET","https://stat6.y.qq.com/pc/fcgi-bin/cgi_music_webreport.fcg?"+query.Encode(),nil)
	if err != nil { return 0 }
	request.Header.Set("User-Agent",api.UserAgent)
	request.Header.Set("Referer","https://y.qq.com/")
	request.Header.Set("Cookie",c.GetCookies())
	httpClient := &http.Client{Timeout:8*time.Second,CheckRedirect:func(req *http.Request,via []*http.Request)error{return http.ErrUseLastResponse}}
	response,err:=httpClient.Do(request)
	if err!=nil { return 0 }
	defer response.Body.Close()
	io.Copy(io.Discard,io.LimitReader(response.Body,65536))
	return C.int(response.StatusCode)
}

func listeningQuery(id,seconds,startedAt,uin int64,guid string)url.Values {
	return url.Values{
		"Count":{"1"},"Fqq":{strconv.FormatInt(uin,10)},"Fguid":{guid},
		"Ffromtag1":{"10050"},"Ffromtag2":{strconv.FormatInt(id,10)},
		"Fsong_id":{strconv.FormatInt(id,10)},"Fplay_time":{strconv.FormatInt(seconds,10)},
		"Fstart_time":{strconv.FormatInt(startedAt,10)},"Ftype":{"3"},"Fversion":{"1"},"Fid1":{"0"},
	}
}
