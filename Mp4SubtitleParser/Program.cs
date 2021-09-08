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
                    Console.WriteLine("Mp4SubtitleParser <segments dir> <segments search pattern>");
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
                var items = Directory.EnumerateFiles(dir, search);

                if (!File.Exists($"{dir}\\init.mp4"))
                    throw new Exception(Path.GetFullPath($"{dir}\\init.mp4") + "not exists!");

                var data = File.ReadAllBytes($"{dir}\\init.mp4");

                //vtt
                var tmp = VTTAction.CheckInit(data);
                if (tmp.Item1)
                {
                    VTTAction.DoWork(data, items, args, tmp.Item2);
                }
                //ttml
                else if (TTMLAction.CheckInit(data))
                {
                    TTMLAction.DoWork(data, items, args);
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
