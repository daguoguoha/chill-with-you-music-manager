package main
import "testing"
func TestListeningQuery(t *testing.T){
	q:=listeningQuery(123,17,1780000000,456,"synthetic-device")
	if q.Get("Fplay_time")!="17" || q.Get("Fsong_id")!="123" || q.Get("Fstart_time")!="1780000000" || q.Get("Fqq")!="456" || q.Get("Count")!="1"{t.Fatal("incorrect genuine playback telemetry")}
}
