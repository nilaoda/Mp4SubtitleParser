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
Mp4SubtitleParser <segments dir> <segments search pattern> [output name] [--segTimeMs=SEGMENT_DUR_IN_MS]
```

# wvtt example
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

```
Mp4SubtitleParser.exe samples-vtt *.mp4
```

you got `output.vtt`

# TTML example
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

```
Mp4SubtitleParser.exe samples-ttmls *.mp4
```

you got `output.ttml` and `output.srt`


# time offset for TTML

in that case, every segment's basetime is `00:00:00.000`...

[sample](https://github.com/nilaoda/Mp4SubtitleParser/blob/main/samples/samples-ttml(no%20init%2C%20need%20offset).zip)

(put any ttml `init.mp4` to the folder, so program can recognize ttml header)

```
Mp4SubtitleParser.exe "samples-ttml(no init, need offset)" *.mp4 --segTimeMs=60000
```

segment-01 will add offset `+0s`  
segment-02 will add offset `+60s`  
segment-03 will add offset `+120s`  
...
