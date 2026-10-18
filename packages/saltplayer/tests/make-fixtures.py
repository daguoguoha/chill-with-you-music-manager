from pathlib import Path
import wave,math,array
root=Path(__file__).resolve().parent/'test-data';music=root/'music';music.mkdir(parents=True,exist_ok=True)
def write(path,seconds,rate=22050,stereo=False):
 path.parent.mkdir(parents=True,exist_ok=True)
 samples=array.array('h');notes=[261.63,329.63,392,523.25,440,392,329.63,293.66]
 for i in range(rate):
  note=notes[int(i/(rate/8))%8];phase=(i%(rate//8))/(rate//8);envelope=min(1,phase*12)*max(0,1-phase)
  value=int(3800*envelope*math.sin(2*math.pi*note*i/rate));samples.append(value)
  if stereo:samples.append(value)
 with wave.open(str(path),'wb') as f:
  f.setnchannels(2 if stereo else 1);f.setsampwidth(2);f.setframerate(rate)
  for _ in range(seconds):f.writeframesraw(samples.tobytes())
for path in [music/'原创测试 - 旋律01.wav',music/'原创测试 - 旋律02.wav',music/'A/same.wav',music/'B/same.wav']:
 if not path.exists():write(path,120)
(root/'椒盐演示歌单.txt').write_text('/storage/emulated/0/Music/原创测试 - 旋律01.wav\n/storage/emulated/0/Music/原创测试 - 旋律02.wav\n',encoding='utf8')
(root/'mixed.txt').write_text('/storage/emulated/0/Music/原创测试 - 旋律01.wav\n/storage/emulated/0/Music/A/same.wav\n/storage/emulated/0/Music/missing.wav\n/storage/emulated/0/Music/same.wav\nhttps://evil.invalid/remote.mp3\n',encoding='utf8')
(root/'m3u8.m3u8').write_text('#EXTM3U\n#EXTINF:120,测试作者 - 第一首\nmusic/原创测试 - 旋律01.wav\n#EXTINF:120,测试作者 - 第二首\nmusic/原创测试 - 旋律02.wav\n#EXTINF:120,测试作者 - 第一首重现\nmusic/原创测试 - 旋律01.wav\n',encoding='utf8')
(root/'utf16.txt').write_text('music/原创测试 - 旋律01.wav\n',encoding='utf16')
(root/'gb18030.txt').write_text('music/原创测试 - 旋律02.wav\n',encoding='gb18030')
(root/'net.m3u8').write_text('#EXTM3U\n#EXT-X-VERSION:3\nhttps://evil.invalid/a.ts\n',encoding='utf8')
(root/'only-missing.txt').write_text('no-such.wav\n',encoding='utf8')
large=root/'long-test.wav'
if not large.exists():write(large,780,48000,True)
print('Generated original test melodies, playlist encodings, remap fixtures, and 149 MB long WAV')
