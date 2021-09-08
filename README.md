# Mp4SubtitleParser

Program to extract embed wvtt/ttml subtitle in mp4.

Translated from shaka-player project.

python/js prj: https://github.com/xhlove/dash-subtitle-extractor

# how to split single-file

```
"C:\Bento4-SDK-1-6-0-639.x86_64-microsoft-win32\bin\mp4split.exe" single-file.mp4
```

# useage
```
Mp4SubtitleParser <segments dir> <segments search pattern> [OUTNAME]
```

For wvtt:
```
│ Mp4SubtitleParser.exe
└─samples-vtt
        init.mp4
        segment-1.0001.mp4
        segment-1.0002.mp4
        segment-1.0003.mp4
        segment-1.0004.mp4
        ...
```

`Mp4SubtitleParser.exe samples-vtt *.mp4`

you got `output.vtt`

---

For ttml:
```
│ Mp4SubtitleParser.exe
└─samples-ttml
        init.mp4
        segment-1.0001.mp4
        segment-1.0002.mp4
        segment-1.0003.mp4
        segment-1.0004.mp4
        ...
```

`Mp4SubtitleParser.exe samples-ttmls *.mp4`

you got `output.ttml` and `output.srt`


