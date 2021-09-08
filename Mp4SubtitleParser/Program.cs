using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Mp4SubtitleParser
{
    class Program
    {
        static void Error(string msg)
        {
            Console.BackgroundColor = ConsoleColor.Red;
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(msg);
            Console.ResetColor();
            Console.WriteLine();
        }

        static void Main(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    Error("arg missing...");
                    Console.WriteLine("Mp4SubtitleParser <segments dir> <segments search pattern> [output name] [--segTimeMs=SEGMENT_DUR_IN_MS]");
                    return;
                }

                var dir = args[0];
                if (string.IsNullOrEmpty(dir))
                    dir = Environment.CurrentDirectory;
                if (!Directory.Exists(dir))
                    throw new Exception("Directory not exists");

                var search = args[1];
                if (string.IsNullOrEmpty(search))
                    throw new Exception("Search pattern should not be empty");

                var outName = "output";
                if (args.Length > 2 && !args[2].StartsWith("--segTimeMs=")) 
                    outName = args[2];

                var items = Directory.EnumerateFiles(dir, search);

                if (!File.Exists($"{dir}\\init.mp4"))
                    throw new Exception(Path.GetFullPath($"{dir}\\init.mp4") + "not exists!");

                var data = File.ReadAllBytes($"{dir}\\init.mp4");

                //offset for per segment ( startTime + index * segTimeMs)
                long segTimeMs = 0;
                if (Environment.GetCommandLineArgs().Any(a => a.StartsWith("--segTimeMs="))) 
                {
                    try
                    {
                        var arg = Environment.GetCommandLineArgs().First(a => a.StartsWith("--segTimeMs=")).Replace("--segTimeMs=", "");
                        segTimeMs = Convert.ToInt64(arg);
                    }
                    catch (Exception) { }
                }

                //vtt
                var tmp = VTTAction.CheckInit(data);
                if (tmp.Item1)
                {
                    VTTAction.DoWork(items, tmp.Item2, outName);
                }
                //ttml
                else if (TTMLAction.CheckInit(data))
                {
                    TTMLAction.DoWork(items, outName, segTimeMs);
                }
                else
                {
                    Console.WriteLine("Can not parse wvtt/ttml subtitle from your init.mp4!!");
                }
            }
            catch (Exception ex)
            {
                Error(ex.Message);
            }
        }
    }
}
