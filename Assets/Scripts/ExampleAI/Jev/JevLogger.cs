using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;

namespace Jev
{
    /// <summary>
    /// Writes one JSON object per line to Logs/Jev/&lt;tank&gt;_&lt;team&gt;_&lt;timestamp&gt;.jsonl so a
    /// match can be analysed offline (latency, confidence, decisions vs. state). Logs/ is git-ignored.
    /// </summary>
    public class JevLogger : IDisposable
    {
        private StreamWriter m_Writer;

        public string FilePath { get; private set; }
        public bool Enabled => m_Writer != null;

        public JevLogger(string tankName, string team, JevConfig config)
        {
            if (!config.EnableLogging)
            {
                return;
            }
            try
            {
                var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", config.LogDirectory));
                Directory.CreateDirectory(dir);
                var safeName = Regex.Replace(tankName ?? "tank", @"[^A-Za-z0-9_\-]", "_");
                FilePath = Path.Combine(dir, $"{safeName}_{team}_{DateTime.Now:yyyyMMdd_HHmmss}.jsonl");
                m_Writer = new StreamWriter(FilePath, false, new UTF8Encoding(false)) { AutoFlush = true };
                Debug.Log($"[Jev] Logging decisions to {FilePath}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Jev] Logging disabled, could not open log file: {e.Message}");
                m_Writer = null;
            }
        }

        public void Log(object record)
        {
            if (m_Writer == null)
            {
                return;
            }
            try
            {
                m_Writer.WriteLine(JsonConvert.SerializeObject(record, Formatting.None));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Jev] Failed to write log line: {e.Message}");
            }
        }

        public void Dispose()
        {
            if (m_Writer == null)
            {
                return;
            }
            try
            {
                m_Writer.Flush();
                m_Writer.Dispose();
            }
            catch (Exception)
            {
                // nothing sensible to do on shutdown
            }
            m_Writer = null;
        }
    }
}
