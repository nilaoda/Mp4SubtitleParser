using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mp4SubtitleParser
{
    class VTTAction
    {
        public static (bool, uint) CheckInit(byte[] data)
        {
            uint timescale = 0;
            bool sawWVTT = false;

            //parse init
            new MP4Parser()
                .Box("moov", MP4Parser.Children)
                .Box("trak", MP4Parser.Children)
                .Box("mdia", MP4Parser.Children)
                .FullBox("mdhd", (box) =>
                {
                    if (!(box.Version == 0 || box.Version == 1))
                        throw new Exception("MDHD version can only be 0 or 1");
                    timescale = MP4Parser.ParseMDHD(box.Reader, box.Version);
                })
                .Box("minf", MP4Parser.Children)
                .Box("stbl", MP4Parser.Children)
                .FullBox("stsd", MP4Parser.SampleDescription)
                .Box("wvtt", (box) => {
                    // A valid vtt init segment, though we have no actual subtitles yet.
                    sawWVTT = true;
                })
                .Parse(data);

            return (sawWVTT, timescale);
        }

        public static void DoWork(IEnumerable<string> items, uint timescale, string outName)
        {
            if (timescale == 0)
                throw new Exception("Missing timescale for VTT content!");

            Console.WriteLine($"timescale: {timescale}");

            List<Cue> cues = new List<Cue>();

            foreach (var item in items)
            {
                if (Path.GetFileName(item) == "init.mp4")
                    continue;
                var dataSeg = File.ReadAllBytes(item);

                bool sawTFDT = false;
                bool sawTRUN = false;
                bool sawMDAT = false;
                byte[] rawPayload = null;
                ulong baseTime = 0;
                ulong defaultDuration = 0;
                List<Sample> presentations = new List<Sample>();


                //parse media
                new MP4Parser()
                    .Box("moof", MP4Parser.Children)
                    .Box("traf", MP4Parser.Children)
                    .FullBox("tfdt", (box) =>
                    {
                        sawTFDT = true;
                        if (!(box.Version == 0 || box.Version == 1))
                            throw new Exception("TFDT version can only be 0 or 1");
                        baseTime = MP4Parser.ParseTFDT(box.Reader, box.Version);
                    })
                    .FullBox("tfhd", (box) =>
                    {
                        if (box.Flags == 1000)
                            throw new Exception("A TFHD box should have a valid flags value");
                        defaultDuration = MP4Parser.ParseTFHD(box.Reader, box.Flags).DefaultSampleDuration;
                    })
                    .FullBox("trun", (box) =>
                    {
                        sawTRUN = true;
                        if (box.Version == 1000)
                            throw new Exception("A TRUN box should have a valid version value");
                        if (box.Flags == 1000)
                            throw new Exception("A TRUN box should have a valid flags value");
                        presentations = MP4Parser.ParseTRUN(box.Reader, box.Version, box.Flags).SampleData;
                    })
                    .Box("mdat", MP4Parser.AllData((data) =>
                    {
                        if (sawMDAT)
                            throw new Exception("VTT cues in mp4 with multiple MDAT are not currently supported");
                        sawMDAT = true;
                        rawPayload = data;
                    }))
                    .Parse(dataSeg,/* partialOkay= */ false);

                if (!sawMDAT && !sawTFDT && !sawTRUN)
                {
                    throw new Exception("A required box is missing");
                }

                var currentTime = baseTime;
                var reader = new BinaryReader2(new MemoryStream(rawPayload));

                foreach (var presentation in presentations)
                {
                    var duration = presentation.SampleDuration == 0 ? defaultDuration : presentation.SampleDuration;
                    var startTime = presentation.SampleCompositionTimeOffset != 0 ?
                          baseTime + presentation.SampleCompositionTimeOffset :
                          currentTime;
                    currentTime = startTime + duration;
                    var totalSize = 0;
                    do
                    {
                        // Read the payload size.
                        var payloadSize = (int)reader.ReadUInt32();
                        totalSize += payloadSize;

                        // Skip the type.
                        var payloadType = reader.ReadUInt32();
                        var payloadName = MP4Parser.TypeToString(payloadType);

                        // Read the data payload.
                        byte[] payload = null;
                        if (payloadName == "vttc")
                        {
                            if (payloadSize > 8)
                            {
                                payload = reader.ReadBytes(payloadSize - 8);
                            }
                        }
                        else if (payloadName == "vtte")
                        {
                            // It's a vtte, which is a vtt cue that is empty. Ignore any data that
                            // does exist.
                            reader.ReadBytes(payloadSize - 8);
                        }
                        else
                        {
                            Console.WriteLine($"Unknown box {payloadName}! Skipping!");
                            reader.ReadBytes(payloadSize - 8);
                        }

                        if (duration != 0)
                        {
                            if (payload != null)
                            {
                                if (timescale == 0)
                                    throw new Exception("Timescale should not be zero!");
                                var cue = ParseVTTC(
                                    payload,
                                    0 + (double)startTime / timescale,
                                    0 + (double)currentTime / timescale);
                                //Check if same subtitle has been splitted
                                if (cue != null)
                                {
                                    var index = cues.FindLastIndex(s => s.EndTime == cue.StartTime && s.Settings == cue.Settings && s.Payload == cue.Payload);
                                    if (index != -1)
                                    {
                                        cues[index].EndTime = cue.EndTime;
                                    }
                                    else
                                    {
                                        cues.Add(cue);
                                    }
                                }
                            }
                        }
                        else
                        {
                            throw new Exception("WVTT sample duration unknown, and no default found!");
                        }

                        if (!(presentation.SampleSize == 0 || totalSize <= presentation.SampleSize))
                        {
                            throw new Exception("The samples do not fit evenly into the sample sizes given in the TRUN box!");
                        }

                    } while (presentation.SampleSize != 0 && (totalSize < presentation.SampleSize));

                    if (reader.HasMoreData())
                    {
                        //throw new Exception("MDAT which contain VTT cues and non-VTT data are not currently supported!");
                    }
                }
            }

            if (cues.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("WEBVTT");
                sb.AppendLine("");
                sb.AppendLine("");
                foreach (var cue in cues)
                {
                    sb.AppendLine($"{ConvertTime(cue.StartTime)} --> {ConvertTime(cue.EndTime)} {(string.IsNullOrEmpty(cue.Settings) ? "position:50% line:94% align:middle" : cue.Settings)}");
                    sb.AppendLine(cue.Payload);
                    sb.AppendLine();
                    //Console.WriteLine($"{ConvertTime(cue.StartTime)} --> {ConvertTime(cue.EndTime)} {(string.IsNullOrEmpty(cue.Settings) ? "position:50% line:94% align:middle" : cue.Settings)}\r\n" +
                    //    $"{cue.Payload}\r\n");
                }

                File.WriteAllText(outName + ".vtt", sb.ToString(), new UTF8Encoding(false));
                Console.WriteLine("Done: " + Path.GetFullPath(outName + ".vtt"));
            }
        }

        private static Cue ParseVTTC(byte[] data, double startTime, double endTime)
        {
            string payload = null;
            string id = null;
            string settings = null;
            new MP4Parser()
                .Box("payl", MP4Parser.AllData((data) =>
                {
                    payload = Encoding.UTF8.GetString(data);
                }))
                .Box("iden", MP4Parser.AllData((data) =>
                {
                    id = Encoding.UTF8.GetString(data);
                }))
                .Box("sttg", MP4Parser.AllData((data) =>
                {
                    settings = Encoding.UTF8.GetString(data);
                }))
                .Parse(data);

            if (payload != null)
            {
                return new Cue() { StartTime = startTime, EndTime = endTime, Payload = payload, Settings = settings };
            }
            return null;
        }


        private static string ConvertTime(double time)
        {
            string subfix = time.ToString("#.000").Split('.').Last();
            TimeSpan ts = new TimeSpan(0, 0, (int)time);
            string str = ts.Hours.ToString("00") + ":" + ts.Minutes.ToString("00") + ":" + ts.Seconds.ToString("00") + "." + subfix;
            return str;
        }
    }
}
