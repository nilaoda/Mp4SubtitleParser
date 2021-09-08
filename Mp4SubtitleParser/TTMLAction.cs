using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Mp4SubtitleParser
{
    class SubEntity
    {
        public string Begin { get; set; }
        public string End { get; set; }
        public string Region { get; set; }
        public List<XmlElement> Contents { get; set; } = new List<XmlElement>();
        public List<string> ContentStrings { get; set; } = new List<string>();
    }

    class TTMLAction
    {
        public static bool CheckInit(byte[] data)
        {
            bool sawSTPP = false;

            //parse init
            new MP4Parser()
                .Box("moov", MP4Parser.Children)
                .Box("trak", MP4Parser.Children)
                .Box("mdia", MP4Parser.Children)
                .Box("minf", MP4Parser.Children)
                .Box("stbl", MP4Parser.Children)
                .FullBox("stsd", MP4Parser.SampleDescription)
                .Box("stpp", (box) => {
                    sawSTPP = true;
                })
                .Parse(data);

            return sawSTPP;
        }

        private static string ShiftTime(string xmlSrc, long segTimeMs, int index)
        {
            string Add(string xmlTime)
            {
                var dt = DateTime.ParseExact(xmlTime, "HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);
                var ts = TimeSpan.FromMilliseconds(dt.TimeOfDay.TotalMilliseconds + segTimeMs * index);
                return string.Format("{0:00}:{1:00}:{2:00}.{3:000}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds);
            }

            if (!xmlSrc.Contains("<?xml") || !xmlSrc.Contains("<head>")) return xmlSrc;
            var xmlDoc = new XmlDocument();
            XmlNamespaceManager nsMgr = null;
            xmlDoc.LoadXml(xmlSrc);
            var ttNode = xmlDoc.LastChild;
            if (nsMgr == null)
            {
                var ns = ((XmlElement)ttNode).GetAttribute("xmlns");
                nsMgr = new XmlNamespaceManager(xmlDoc.NameTable);
                nsMgr.AddNamespace("ns", ns);
            }

            var bodyNode = ttNode.SelectSingleNode("ns:body", nsMgr);
            if (bodyNode == null)
                return xmlSrc;

            var _div = bodyNode.SelectSingleNode("ns:div", nsMgr);
            //Parse <p> label
            foreach (XmlElement _p in _div.SelectNodes("ns:p", nsMgr))
            {
                var _begin = _p.GetAttribute("begin");
                var _end = _p.GetAttribute("end");
                _p.SetAttribute("begin", Add(_begin));
                _p.SetAttribute("end", Add(_end));
                //Console.WriteLine($"{_begin} {_p.GetAttribute("begin")}");
                //Console.WriteLine($"{_end} {_p.GetAttribute("begin")}");
            }

            return xmlDoc.OuterXml;
        }

        public static void DoWork(IEnumerable<string> items, string outName, long segTimeMs)
        {
            //read ttmls
            List<string> xmls = new List<string>();
            int segIndex = 0;
            foreach (var item in items)
            {
                if (Path.GetFileName(item) == "init.mp4")
                    continue;
                var dataSeg = File.ReadAllBytes(item);

                var sawMDAT = false;
                //parse media
                new MP4Parser()
                    .Box("mdat", MP4Parser.AllData((data) =>
                    {
                        sawMDAT = true;
                        // Join this to any previous payload, in case the mp4 has multiple
                        // mdats.
                        if (segTimeMs != 0)
                        {
                            xmls.Add(ShiftTime(Encoding.UTF8.GetString(data), segTimeMs, segIndex));
                        }
                        else
                        {
                            xmls.Add(Encoding.UTF8.GetString(data));
                        }
                    }))
                    .Parse(dataSeg,/* partialOkay= */ false);
                segIndex++;
            }


            //parsing
            var xmlDoc = new XmlDocument();
            var finalSubs = new List<SubEntity>();
            XmlNode headNode = null;
            XmlNamespaceManager nsMgr = null;
            foreach (var item in xmls)
            {
                var xmlContent = item;
                if (!xmlContent.Contains("<?xml") || !xmlContent.Contains("<head>")) continue;

                xmlDoc.LoadXml(xmlContent);
                var ttNode = xmlDoc.LastChild;
                if (nsMgr == null)
                {
                    var ns = ((XmlElement)ttNode).GetAttribute("xmlns");
                    nsMgr = new XmlNamespaceManager(xmlDoc.NameTable);
                    nsMgr.AddNamespace("ns", ns);
                }
                if (headNode == null)
                    headNode = ttNode.SelectSingleNode("ns:head", nsMgr);

                var bodyNode = ttNode.SelectSingleNode("ns:body", nsMgr);
                if (bodyNode == null)
                    continue;

                var _div = bodyNode.SelectSingleNode("ns:div", nsMgr);
                //Parse <p> label
                foreach (XmlElement _p in _div.SelectNodes("ns:p", nsMgr))
                {
                    var _begin = _p.GetAttribute("begin");
                    var _end = _p.GetAttribute("end");
                    var _region = _p.GetAttribute("region");
                    var sub = new SubEntity
                    {
                        Begin = _begin,
                        End = _end,
                        Region = _region
                    };
                    var _spans = _p.ChildNodes;
                    //Collect <span>
                    foreach (XmlNode _node in _spans)
                    {
                        if (_node.NodeType == XmlNodeType.Element)
                        {
                            var _span = (XmlElement)_node;
                            if (string.IsNullOrEmpty(_span.InnerText))
                                continue;
                            sub.Contents.Add(_span);
                            sub.ContentStrings.Add(_span.OuterXml);
                        }
                        else if (_node.NodeType == XmlNodeType.Text)
                        {
                            var _span = new XmlDocument().CreateElement("span");
                            _span.InnerText = _node.Value;
                            sub.Contents.Add(_span);
                            sub.ContentStrings.Add(_span.OuterXml);
                        }
                    }
                    //Check if one <p> has been splitted
                    var index = finalSubs.FindLastIndex(s => s.End == _begin && s.Region == _region && s.ContentStrings.SequenceEqual(sub.ContentStrings));
                    //Skip empty lines
                    if (sub.ContentStrings.Count > 0)
                    {
                        //Extend <p> duration
                        if (index != -1)
                            finalSubs[index].End = sub.End;
                        else
                            finalSubs.Add(sub);
                    }
                }
            }


            //Generate TTML...
            StringBuilder xml = new StringBuilder(@$"<?xml version=""1.0"" encoding=""utf-8""?>
<tt
    xmlns=""http://www.w3.org/ns/ttml""
    xmlns:smpte=""http://www.smpte-ra.org/schemas/2052-1/2010/smpte-tt""
    xmlns:ttm=""http://www.w3.org/ns/ttml#metadata""
    xmlns:tts=""http://www.w3.org/ns/ttml#styling"" xml:lang=""eng"">
{string.Join("\r\n   ", headNode.OuterXml.Split('\n'))}
");
            xml.AppendLine("    <body>");
            xml.AppendLine("        <div>");
            foreach (var sub in finalSubs)
            {
                xml.AppendLine($"            <p begin=\"{sub.Begin}\" end=\"{sub.End}\" region=\"{sub.Region}\">");
                foreach (var item in sub.ContentStrings)
                {
                    xml.AppendLine($"                {item}");
                }
                xml.AppendLine("            </p>");
            }
            xml.AppendLine("        </div>");
            xml.AppendLine("    </body>");
            xml.AppendLine("</tt>");


            File.WriteAllText(outName + ".ttml", xml.ToString(), new UTF8Encoding(false));
            Console.WriteLine("Done: " + Path.GetFullPath(outName + ".ttml"));



            //Generate SRT...
            var dic = new Dictionary<string, string>();
            foreach (var sub in finalSubs)
            {
                var key = $"{sub.Begin.Replace(".", ",")} --> {sub.End.Replace(".", ",")}";
                foreach (var item in sub.Contents)
                {
                    if (dic.ContainsKey(key))
                    {
                        if (item.GetAttribute("tts:fontStyle") == "italic" || item.GetAttribute("tts:fontStyle") == "oblique")
                            dic[key] = $"{dic[key]}\r\n<i>{item.InnerText.Trim()}</i>";
                        else
                            dic[key] = $"{dic[key]}\r\n{item.InnerText.Trim()}";
                    }
                    else
                    {
                        if (item.GetAttribute("tts:fontStyle") == "italic" || item.GetAttribute("tts:fontStyle") == "oblique")
                            dic.Add(key, $"<i>{item.InnerText.Trim()}</i>");
                        else
                            dic.Add(key, item.InnerText.Trim());
                    }
                }
            }


            StringBuilder srt = new StringBuilder();
            int i = 1;
            foreach (var item in dic)
            {
                srt.AppendLine($"{i++}");
                srt.AppendLine(item.Key);
                srt.AppendLine(item.Value);
                srt.AppendLine();
            }


            File.WriteAllText(outName + ".srt", srt.ToString(), new UTF8Encoding(false));
            Console.WriteLine("Done: " + Path.GetFullPath(outName + ".srt"));
            Console.WriteLine();
        }
    }
}
