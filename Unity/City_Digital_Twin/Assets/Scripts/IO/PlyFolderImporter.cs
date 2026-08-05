using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CityDigitalTwin.IO
{
    /// <summary>
    /// Bulk-reads every PLY in a folder into memory, without touching the scene.
    ///
    /// Separated from scene placement on purpose: this is the "get the data in"
    /// half, so a folder can be parsed, counted and validated before anything is
    /// rendered. <see cref="CityMeshLoader"/> is the half that places the results
    /// at their real-world coordinates.
    ///
    /// Sized for the 500 m grid run: 2211 files / 2.67M vertices / ~117 MB. Parsing
    /// is parallelised across cores because the work is CPU-bound and every file is
    /// independent. None of this touches the Unity API, so it is safe off the main
    /// thread — mesh creation happens later, on the main thread.
    /// </summary>
    public static class PlyFolderImporter
    {
        /// <summary>One parsed file, with the group id inferred from its filename.</summary>
        public sealed class Entry
        {
            public string Path;
            public string FileName;
            /// <summary>Filename minus the "_lodN" suffix, e.g. "grid_000_018".</summary>
            public string GroupId;
            public string Lod;
            public PlyMeshData Data;
            public Exception Error;
            public bool Ok => Error == null && Data != null;
        }

        /// <summary>Aggregate result of a folder import.</summary>
        public sealed class Result
        {
            public readonly List<Entry> Entries = new List<Entry>();
            public string Folder;
            public long ElapsedMs;

            public int OkCount { get; internal set; }
            public int FailCount { get; internal set; }
            public long TotalVertices { get; internal set; }
            public long TotalTriangles { get; internal set; }

            /// <summary>Successfully parsed entries, keyed by group id.</summary>
            public Dictionary<string, Entry> ByGroupId()
            {
                var map = new Dictionary<string, Entry>(Entries.Count, StringComparer.Ordinal);
                foreach (var e in Entries)
                    if (e.Ok && !string.IsNullOrEmpty(e.GroupId)) map[e.GroupId] = e;
                return map;
            }

            public string Summary()
            {
                return $"{OkCount}/{Entries.Count} PLY files parsed" +
                       (FailCount > 0 ? $" ({FailCount} FAILED)" : "") +
                       $", {TotalVertices:N0} verts, {TotalTriangles:N0} tris, {ElapsedMs} ms";
            }
        }

        /// <summary>
        /// Parse every *.ply in <paramref name="folder"/>.
        /// </summary>
        /// <param name="folder">Absolute path, or one relative to the Unity project root.</param>
        /// <param name="settings">Import settings; defaults are correct for pipeline output.</param>
        /// <param name="searchPattern">Filename filter, e.g. "*_lod1.ply" to take one LOD only.</param>
        /// <param name="recursive">Include subfolders.</param>
        /// <param name="parallel">Parse across cores. Turn off to make error ordering deterministic.</param>
        /// <param name="progress">Optional 0..1 callback. Invoked from worker threads — do not
        /// touch the Unity API inside it.</param>
        public static Result ImportFolder(string folder,
                                          PlyImportSettings settings = null,
                                          string searchPattern = "*.ply",
                                          bool recursive = false,
                                          bool parallel = true,
                                          Action<float> progress = null)
        {
            settings = settings ?? PlyImportSettings.Default;

            string resolved = ProjectPaths.Resolve(folder);
            if (string.IsNullOrEmpty(resolved) || !Directory.Exists(resolved))
                throw new DirectoryNotFoundException(
                    $"PLY folder not found: '{resolved ?? folder}'. Pipeline output normally " +
                    "lives in Processed_data/<run>/mesh_files/.");

            string[] files = Directory.GetFiles(resolved, searchPattern,
                recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.Ordinal);   // stable, reproducible order

            var result = new Result { Folder = resolved };
            var entries = new Entry[files.Length];
            var sw = Stopwatch.StartNew();
            int done = 0;

            void ParseOne(int i)
            {
                var entry = new Entry
                {
                    Path = files[i],
                    FileName = Path.GetFileName(files[i])
                };
                SplitGroupAndLod(entry.FileName, out entry.GroupId, out entry.Lod);

                try
                {
                    entry.Data = PlyParser.Load(files[i], settings);
                }
                catch (Exception e)
                {
                    // One bad file must not abort a 2000-file import; record and continue.
                    entry.Error = e;
                }

                entries[i] = entry;

                if (progress != null)
                {
                    int n = Interlocked.Increment(ref done);
                    progress((float)n / Math.Max(1, files.Length));
                }
            }

            if (parallel && files.Length > 1)
                Parallel.For(0, files.Length, ParseOne);
            else
                for (int i = 0; i < files.Length; i++) ParseOne(i);

            sw.Stop();
            result.ElapsedMs = sw.ElapsedMilliseconds;

            foreach (var e in entries)
            {
                if (e == null) continue;
                result.Entries.Add(e);
                if (e.Ok)
                {
                    result.OkCount++;
                    result.TotalVertices += e.Data.VertexCount;
                    result.TotalTriangles += e.Data.TriangleCount;
                }
                else
                {
                    result.FailCount++;
                }
            }

            return result;
        }

        /// <summary>
        /// Split "grid_000_018_lod1.ply" into group "grid_000_018" and lod "lod1".
        /// Falls back to the whole stem when there is no recognisable LOD suffix.
        /// </summary>
        public static void SplitGroupAndLod(string fileName, out string groupId, out string lod)
        {
            string stem = Path.GetFileNameWithoutExtension(fileName) ?? "";
            int i = stem.LastIndexOf("_lod", StringComparison.OrdinalIgnoreCase);
            if (i > 0)
            {
                groupId = stem.Substring(0, i);
                lod = stem.Substring(i + 1);
            }
            else
            {
                groupId = stem;
                lod = null;
            }
        }

        /// <summary>
        /// Log a short report, including the first few failures. Main thread only.
        /// </summary>
        public static void LogReport(Result result, int maxFailuresShown = 5)
        {
            Debug.Log($"[PlyFolderImporter] {result.Folder}\n  {result.Summary()}");

            if (result.FailCount == 0) return;

            int shown = 0;
            foreach (var e in result.Entries)
            {
                if (e.Ok) continue;
                Debug.LogError($"[PlyFolderImporter] FAILED {e.FileName}: {e.Error.Message}");
                if (++shown >= maxFailuresShown) break;
            }
            if (result.FailCount > shown)
                Debug.LogError($"[PlyFolderImporter] ...and {result.FailCount - shown} more failures.");
        }
    }
}
