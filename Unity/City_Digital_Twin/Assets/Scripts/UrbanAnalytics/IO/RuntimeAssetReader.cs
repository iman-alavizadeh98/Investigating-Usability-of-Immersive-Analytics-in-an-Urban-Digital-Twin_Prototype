using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace UrbanAnalytics.Core.IO
{
    /// <summary>
    /// Reads runtime package files from StreamingAssets.
    ///
    /// Supports:
    /// - Unity Editor
    /// - Windows builds
    /// - Android / Meta Quest standalone builds
    ///
    /// Callers always use paths relative to StreamingAssets.
    ///
    /// Examples:
    /// project_manifest.json
    /// spatial_layers/ruta_250/layer.json
    /// context/buildings/buildings.bin
    /// </summary>
    public sealed class RuntimeAssetReader
    {
        /// <summary>
        /// Reads a UTF-8 text asset.
        /// </summary>
        public async Task<string> ReadTextAsync(
            string relativePath,
            CancellationToken cancellationToken = default
        )
        {
            byte[] bytes =
                await ReadBytesAsync(
                    relativePath,
                    cancellationToken
                );

            return DecodeUtf8(
                bytes,
                relativePath
            );
        }


        /// <summary>
        /// Reads a binary asset.
        ///
        /// This will later also support files such as
        /// prepared building geometry or binary data layers.
        /// </summary>
        public async Task<byte[]> ReadBytesAsync(
            string relativePath,
            CancellationToken cancellationToken = default
        )
        {
            string normalizedPath =
                NormalizeRelativePath(
                    relativePath
                );

#if UNITY_ANDROID && !UNITY_EDITOR

            return await ReadUsingUnityWebRequestAsync(
                normalizedPath,
                cancellationToken
            );

#else

            return await ReadUsingFileSystemAsync(
                normalizedPath,
                cancellationToken
            );

#endif
        }


        // =========================================================
        // DESKTOP / EDITOR
        // =========================================================

        private static async Task<byte[]> ReadUsingFileSystemAsync(
            string normalizedRelativePath,
            CancellationToken cancellationToken
        )
        {
            string root =
                Path.GetFullPath(
                    Application.streamingAssetsPath
                );


            string fullPath =
                Path.GetFullPath(
                    Path.Combine(
                        root,
                        normalizedRelativePath
                    )
                );


            EnsurePathInsideRoot(
                fullPath,
                root
            );


            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    $"Runtime asset was not found: " +
                    $"'{normalizedRelativePath}'.",
                    fullPath
                );
            }


            cancellationToken.ThrowIfCancellationRequested();


            try
            {
                return await File.ReadAllBytesAsync(
                    fullPath,
                    cancellationToken
                );
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new IOException(
                    $"Failed to read runtime asset " +
                    $"'{normalizedRelativePath}'.",
                    exception
                );
            }
        }


        // =========================================================
        // ANDROID / META QUEST
        // =========================================================

        private static async Task<byte[]> ReadUsingUnityWebRequestAsync(
            string normalizedRelativePath,
            CancellationToken cancellationToken
        )
        {
            string root =
                Application.streamingAssetsPath
                    .TrimEnd('/');


            string uri =
                $"{root}/{normalizedRelativePath}";


            using UnityWebRequest request =
                UnityWebRequest.Get(uri);


            UnityWebRequestAsyncOperation operation =
                request.SendWebRequest();


            while (!operation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    request.Abort();

                    cancellationToken
                        .ThrowIfCancellationRequested();
                }

                await Task.Yield();
            }


            cancellationToken
                .ThrowIfCancellationRequested();


            if (request.result !=
                UnityWebRequest.Result.Success)
            {
                throw new IOException(
                    $"Failed to read runtime asset " +
                    $"'{normalizedRelativePath}' on Android. " +
                    $"UnityWebRequest error: {request.error}"
                );
            }


            byte[] data =
                request.downloadHandler?.data;


            if (data == null)
            {
                throw new IOException(
                    $"Runtime asset " +
                    $"'{normalizedRelativePath}' returned no data."
                );
            }


            return data;
        }


        // =========================================================
        // PATH HANDLING
        // =========================================================

        /// <summary>
        /// Validates and normalizes a package-relative path.
        ///
        /// The runtime package must never use absolute paths
        /// or traversal such as ../../file.json.
        /// </summary>
        public static string NormalizeRelativePath(
            string relativePath
        )
        {
            if (string.IsNullOrWhiteSpace(
                    relativePath
                ))
            {
                throw new ArgumentException(
                    "Runtime asset path cannot be null or empty.",
                    nameof(relativePath)
                );
            }


            string path =
                relativePath
                    .Trim()
                    .Replace('\\', '/');


            if (path.StartsWith(
                    "/",
                    StringComparison.Ordinal
                ))
            {
                throw new ArgumentException(
                    $"Runtime asset path must be relative: " +
                    $"'{relativePath}'.",
                    nameof(relativePath)
                );
            }


            if (Path.IsPathRooted(path))
            {
                throw new ArgumentException(
                    $"Runtime asset path must not be absolute: " +
                    $"'{relativePath}'.",
                    nameof(relativePath)
                );
            }


            string[] segments =
                path.Split(
                    '/',
                    StringSplitOptions.RemoveEmptyEntries
                );


            if (segments.Length == 0)
            {
                throw new ArgumentException(
                    "Runtime asset path contains no valid path segments.",
                    nameof(relativePath)
                );
            }


            foreach (string segment in segments)
            {
                if (segment == "." ||
                    segment == "..")
                {
                    throw new ArgumentException(
                        $"Runtime asset path contains invalid " +
                        $"navigation segment '{segment}': " +
                        $"'{relativePath}'.",
                        nameof(relativePath)
                    );
                }
            }


            return string.Join(
                "/",
                segments
            );
        }


        /// <summary>
        /// Combines two runtime-package-relative paths safely.
        ///
        /// Example:
        ///
        /// spatial_layers/ruta_250/layer.json
        /// +
        /// geometry.json
        ///
        /// becomes:
        ///
        /// spatial_layers/ruta_250/geometry.json
        /// </summary>
        public static string ResolveSiblingPath(
            string sourceFileRelativePath,
            string referencedRelativePath
        )
        {
            string source =
                NormalizeRelativePath(
                    sourceFileRelativePath
                );


            string reference =
                NormalizeRelativePath(
                    referencedRelativePath
                );


            int lastSlash =
                source.LastIndexOf('/');


            string directory =
                lastSlash >= 0
                    ? source.Substring(
                        0,
                        lastSlash
                    )
                    : string.Empty;


            string combined =
                string.IsNullOrEmpty(directory)
                    ? reference
                    : $"{directory}/{reference}";


            return NormalizeRelativePath(
                combined
            );
        }


        private static void EnsurePathInsideRoot(
            string candidatePath,
            string rootPath
        )
        {
            string root =
                Path.GetFullPath(
                    rootPath
                )
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                );


            string candidate =
                Path.GetFullPath(
                    candidatePath
                );


            bool inside =
                candidate.StartsWith(
                    root +
                    Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase
                ) ||
                string.Equals(
                    candidate,
                    root,
                    StringComparison.OrdinalIgnoreCase
                );


            if (!inside)
            {
                throw new IOException(
                    $"Resolved runtime asset path lies outside " +
                    $"StreamingAssets: '{candidate}'."
                );
            }
        }


        // =========================================================
        // TEXT DECODING
        // =========================================================

        private static string DecodeUtf8(
            byte[] bytes,
            string relativePath
        )
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(
                    nameof(bytes)
                );
            }


            int offset = 0;


            // Skip UTF-8 BOM if present.
            if (bytes.Length >= 3 &&
                bytes[0] == 0xEF &&
                bytes[1] == 0xBB &&
                bytes[2] == 0xBF)
            {
                offset = 3;
            }


            try
            {
                var utf8 =
                    new UTF8Encoding(
                        false,
                        true
                    );


                return utf8.GetString(
                    bytes,
                    offset,
                    bytes.Length - offset
                );
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidDataException(
                    $"Runtime text asset " +
                    $"'{relativePath}' is not valid UTF-8.",
                    exception
                );
            }
        }
    }
}