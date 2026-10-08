using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace UrbanAnalytics.Study
{
    /// <summary>
    /// Append-only session log for evaluation sessions.
    ///
    /// One JSON object per line (JSON Lines) in
    ///   &lt;Application.persistentDataPath&gt;/study_logs/&lt;sessionId&gt;.jsonl
    /// plus a one-row-per-answer CSV next to it
    ///   &lt;sessionId&gt;_answers.csv
    ///
    /// Every line: {"t": UTC ISO time, "s": seconds since session
    /// start, "session", "participant", "condition", "event", ...fields}.
    /// Lines are flushed immediately, so a crash loses nothing.
    /// Plain C# (no Unity API besides the folder), so it works the
    /// same for the desktop and the later VR condition.
    /// </summary>
    public sealed class StudyLog : IDisposable
    {
        private readonly StreamWriter writer;

        private readonly StreamWriter answers;

        private readonly DateTime startUtc;


        public string SessionId { get; }

        public string ParticipantId { get; }

        public string Condition { get; }

        public string LogPath { get; }

        public string AnswersPath { get; }


        public StudyLog(
            string folder,
            string participantId,
            string condition
        )
        {
            startUtc =
                DateTime.UtcNow;

            ParticipantId =
                Clean(participantId, "P00");

            Condition =
                Clean(condition, "desktop");

            SessionId =
                $"{startUtc:yyyyMMdd'T'HHmmss}_{ParticipantId}_{Condition}";

            Directory.CreateDirectory(
                folder
            );

            LogPath =
                Path.Combine(folder, SessionId + ".jsonl");

            AnswersPath =
                Path.Combine(folder, SessionId + "_answers.csv");

            writer =
                new StreamWriter(LogPath, true, new UTF8Encoding(false))
                {
                    AutoFlush = true
                };

            answers =
                new StreamWriter(AnswersPath, true, new UTF8Encoding(false))
                {
                    AutoFlush = true
                };

            answers.WriteLine(
                "session,participant,condition,scenario,preset,answer,expected,correct,confidence,seconds,interactions"
            );
        }


        public double Elapsed =>
            (DateTime.UtcNow - startUtc).TotalSeconds;


        /// <summary>
        /// Writes one event. Field values may be string, bool,
        /// int/long/float/double, Vector3 or null.
        /// </summary>
        public void Write(
            string eventName,
            params (string Key, object Value)[] fields
        )
        {
            var line =
                new StringBuilder(256);

            line.Append('{');
            Append(line, "t", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            line.Append(',');
            Append(line, "s", Math.Round(Elapsed, 3));
            line.Append(',');
            Append(line, "session", SessionId);
            line.Append(',');
            Append(line, "participant", ParticipantId);
            line.Append(',');
            Append(line, "condition", Condition);
            line.Append(',');
            Append(line, "event", eventName);

            foreach ((string key, object value) in fields)
            {
                line.Append(',');
                Append(line, key, value);
            }

            line.Append('}');

            writer.WriteLine(
                line.ToString()
            );
        }


        public void WriteAnswer(
            string scenarioId,
            string presetId,
            string answer,
            string expected,
            bool? correct,
            int confidence,
            double seconds,
            int interactions
        )
        {
            answers.WriteLine(string.Join(",", new[]
            {
                Csv(SessionId),
                Csv(ParticipantId),
                Csv(Condition),
                Csv(scenarioId),
                Csv(presetId),
                Csv(answer),
                Csv(expected),
                correct.HasValue ? (correct.Value ? "1" : "0") : "",
                confidence > 0 ? confidence.ToString(CultureInfo.InvariantCulture) : "",
                seconds.ToString("0.###", CultureInfo.InvariantCulture),
                interactions.ToString(CultureInfo.InvariantCulture)
            }));
        }


        public void Dispose()
        {
            writer?.Dispose();
            answers?.Dispose();
        }


        // =========================================================
        // JSON helpers (no dependency; values are simple)
        // =========================================================

        private static void Append(
            StringBuilder line,
            string key,
            object value
        )
        {
            AppendString(line, key);
            line.Append(':');

            switch (value)
            {
                case null:
                    line.Append("null");
                    break;

                case string s:
                    AppendString(line, s);
                    break;

                case bool b:
                    line.Append(b ? "true" : "false");
                    break;

                case int i:
                    line.Append(i.ToString(CultureInfo.InvariantCulture));
                    break;

                case long l:
                    line.Append(l.ToString(CultureInfo.InvariantCulture));
                    break;

                case float f:
                    line.Append(Number(f));
                    break;

                case double d:
                    line.Append(Number(d));
                    break;

                case Vector3 v:
                    line.Append('[')
                        .Append(Number(v.x)).Append(',')
                        .Append(Number(v.y)).Append(',')
                        .Append(Number(v.z)).Append(']');
                    break;

                case IEnumerable<string> list:
                    line.Append('[');
                    bool first = true;
                    foreach (string item in list)
                    {
                        if (!first)
                        {
                            line.Append(',');
                        }

                        AppendString(line, item);
                        first = false;
                    }
                    line.Append(']');
                    break;

                default:
                    AppendString(line, value.ToString());
                    break;
            }
        }


        private static string Number(
            double value
        )
        {
            return double.IsNaN(value) || double.IsInfinity(value)
                ? "null"
                : value.ToString("0.######", CultureInfo.InvariantCulture);
        }


        private static void AppendString(
            StringBuilder line,
            string value
        )
        {
            line.Append('"');

            foreach (char c in value ?? string.Empty)
            {
                switch (c)
                {
                    case '"': line.Append("\\\""); break;
                    case '\\': line.Append("\\\\"); break;
                    case '\n': line.Append("\\n"); break;
                    case '\r': line.Append("\\r"); break;
                    case '\t': line.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            line.Append("\\u").Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            line.Append(c);
                        }
                        break;
                }
            }

            line.Append('"');
        }


        private static string Csv(
            string value
        )
        {
            value ??= string.Empty;

            return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
        }


        private static string Clean(
            string value,
            string fallback
        )
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            var cleaned =
                new StringBuilder();

            foreach (char c in value.Trim())
            {
                cleaned.Append(char.IsLetterOrDigit(c) || c == '-' ? c : '_');
            }

            return cleaned.ToString();
        }
    }
}
