using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace TabletopTempo.Core.Input
{
    /// <summary>
    /// A recorded stream of hand frames (table space) for deterministic tests.
    /// Text format ".ttrec" (compact, diff-friendly, no LFS):
    ///   # ttrec 1 source=&lt;provider&gt; note=&lt;free text&gt;
    ///   t  Ltracked Lconf Lqx Lqy Lqz Lqw  L0x L0y L0z … L20z  Rtracked Rconf Rq… R0x … R20z
    /// One line per frame, space separated, invariant culture, 0.1 mm precision.
    /// </summary>
    public sealed class ReplaySession
    {
        public const int Version = 1;
        private const int HandFields = 2 + 4 + HandData.JointCount * 3;

        public string Source = "unknown";
        public string Note = "";
        public readonly List<HandFrame> Frames = new List<HandFrame>();

        public double Duration => Frames.Count < 2 ? 0 : Frames[Frames.Count - 1].Time - Frames[0].Time;

        public void Add(HandFrame frame)
        {
            var copy = new HandFrame();
            copy.CopyFrom(frame);
            Frames.Add(copy);
        }

        // ------------------------------------------------------------------ text I/O

        public string ToText()
        {
            var sb = new StringBuilder(Frames.Count * 1200);
            sb.Append("# ttrec ").Append(Version).Append(" source=").Append(Clean(Source)).Append(" note=").Append(Clean(Note)).Append('\n');
            foreach (var f in Frames)
            {
                Num(sb, f.Time, "F5");
                Hand(sb, f.Left);
                Hand(sb, f.Right);
                sb.Append('\n');
            }
            return sb.ToString();
        }

        public static ReplaySession FromText(string text)
        {
            var s = new ReplaySession();
            using (var reader = new StringReader(text))
            {
                string line;
                int lineNo = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNo++;
                    line = line.Trim();
                    if (line.Length == 0) continue;
                    if (line[0] == '#')
                    {
                        ParseHeader(s, line);
                        continue;
                    }
                    var parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 1 + 2 * HandFields)
                        throw new FormatException($"ttrec line {lineNo}: expected {1 + 2 * HandFields} values, got {parts.Length}");
                    var f = new HandFrame { Time = D(parts[0]) };
                    int k = 1;
                    ReadHand(parts, ref k, f.Left);
                    ReadHand(parts, ref k, f.Right);
                    s.Frames.Add(f);
                }
            }
            return s;
        }

        private static void ParseHeader(ReplaySession s, string line)
        {
            foreach (var token in line.Substring(1).Split(' '))
            {
                if (token.StartsWith("source=")) s.Source = token.Substring(7);
                else if (token.StartsWith("note=")) s.Note = token.Substring(5).Replace('_', ' ');
            }
        }

        private static void Hand(StringBuilder sb, HandData h)
        {
            sb.Append(h.Tracked ? " 1" : " 0");
            Num(sb, h.Confidence, "F2");
            var q = h.WristRotation;
            Num(sb, q.x, "F4"); Num(sb, q.y, "F4"); Num(sb, q.z, "F4"); Num(sb, q.w, "F4");
            for (int i = 0; i < HandData.JointCount; i++)
            {
                var p = h.Joints[i];
                Num(sb, p.x, "F4"); Num(sb, p.y, "F4"); Num(sb, p.z, "F4");
            }
        }

        private static void ReadHand(string[] parts, ref int k, HandData h)
        {
            h.Tracked = parts[k++] == "1";
            h.Confidence = F(parts[k++]);
            h.WristRotation = new Quaternion(F(parts[k++]), F(parts[k++]), F(parts[k++]), F(parts[k++]));
            for (int i = 0; i < HandData.JointCount; i++)
                h.Joints[i] = new Vector3(F(parts[k++]), F(parts[k++]), F(parts[k++]));
        }

        private static void Num(StringBuilder sb, double v, string fmt) => sb.Append(' ').Append(v.ToString(fmt, CultureInfo.InvariantCulture));
        private static float F(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
        private static double D(string s) => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
        private static string Clean(string s) => string.IsNullOrEmpty(s) ? "-" : s.Replace(' ', '_').Replace('\n', '_');
    }

    /// <summary>
    /// Plays a <see cref="ReplaySession"/> back as a provider. Time-based: frame times are shifted so
    /// the first frame plays at the first <see cref="Write"/> call (or at <see cref="StartAt"/>).
    /// </summary>
    public sealed class ReplayHandProvider : IHandFrameProvider
    {
        private readonly ReplaySession _session;
        private readonly bool _loop;
        private double? _startInputTime;
        private int _cursor;

        public ReplayHandProvider(ReplaySession session, bool loop = false)
        {
            _session = session;
            _loop = loop;
        }

        public string Name => "replay:" + _session.Source;
        public bool OutputsTableSpace => true;
        public bool Finished { get; private set; }

        public void StartAt(double inputTime)
        {
            _startInputTime = inputTime;
            _cursor = 0;
            Finished = false;
        }

        public bool Write(HandFrame target, double inputTime)
        {
            var frames = _session.Frames;
            if (frames.Count == 0) return false;
            if (_startInputTime == null) StartAt(inputTime);

            double t0 = frames[0].Time;
            double local = inputTime - _startInputTime.Value + t0;
            if (_loop && _session.Duration > 0)
                local = t0 + ((local - t0) % _session.Duration);

            while (_cursor + 1 < frames.Count && frames[_cursor + 1].Time <= local) _cursor++;
            if (!_loop && _cursor == frames.Count - 1 && local > frames[_cursor].Time) Finished = true;
            if (_loop && local < frames[_cursor].Time) _cursor = 0;

            target.CopyFrom(frames[_cursor]);
            target.Time = inputTime;
            return true;
        }
    }
}
