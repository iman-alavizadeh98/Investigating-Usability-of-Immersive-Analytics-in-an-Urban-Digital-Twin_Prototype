using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

using UrbanAnalytics.Core;
using UrbanAnalytics.Core.IO;
using UrbanAnalytics.Visualization;

namespace UrbanAnalytics.Interaction
{
    /// <summary>
    /// One entry of StreamingAssets/visualizations/catalog.json.
    /// </summary>
    [Serializable]
    public sealed class VisualizationCatalogEntry
    {
        public string id;

        public string displayName;

        /// <summary>
        /// Spec file, relative to the catalog file.
        /// </summary>
        public string file;
    }


    [Serializable]
    internal sealed class VisualizationCatalogFile
    {
        public List<VisualizationCatalogEntry> visualizations =
            new List<VisualizationCatalogEntry>();
    }


    /// <summary>
    /// A visualization the user can switch to at runtime.
    /// </summary>
    public sealed class VisualizationOption
    {
        public string Id;

        public string DisplayName;

        public VisualizationSpec Spec;

        /// <summary>
        /// "scene" for the Inspector startup spec, otherwise the
        /// catalog file it came from.
        /// </summary>
        public string Source;
    }


    /// <summary>
    /// Switches visualizations at runtime.
    ///
    /// Options = the scene's startup spec (first) + the specs
    /// listed in a catalog in the runtime package
    /// (StreamingAssets/visualizations/catalog.json). Specs are
    /// VisualizationSpec JSON, the same schema as the Inspector.
    ///
    /// Keys: 1–9 apply option 1–9, 0 clears. The UI calls Apply /
    /// Clear directly.
    /// </summary>
    public sealed class VisualizationSwitcher :
        MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField]
        private VisualizationManager visualizationManager;

        [SerializeField]
        private ProjectManager projectManager;


        [Header("Catalog")]
        [Tooltip(
            "Path relative to StreamingAssets. Missing catalog = " +
            "only the startup visualization is offered."
        )]
        [SerializeField]
        private string catalogPath =
            "visualizations/catalog.json";

        [SerializeField]
        private bool includeStartupVisualization =
            true;


        [Header("Input")]
        [SerializeField]
        private bool enableNumberKeys =
            true;


        private readonly List<VisualizationOption> options =
            new List<VisualizationOption>();


        private CancellationTokenSource lifetimeCancellation;

        private int requestCounter;


        public IReadOnlyList<VisualizationOption> Options =>
            options;


        /// <summary>
        /// Index of the option currently shown, -1 when none (or
        /// a spec applied by other code).
        /// </summary>
        public int ActiveIndex
        {
            get;
            private set;
        } = -1;


        public int PendingIndex
        {
            get;
            private set;
        } = -1;


        public bool IsApplying =>
            PendingIndex >= 0;


        public string LastError
        {
            get;
            private set;
        }


        public bool IsLoaded
        {
            get;
            private set;
        }


        public event Action Changed;


        // =========================================================
        // UNITY
        // =========================================================

        private void Awake()
        {
            if (visualizationManager == null)
            {
                visualizationManager =
                    FindFirstObjectByType<VisualizationManager>();
            }


            if (projectManager == null)
            {
                projectManager =
                    FindFirstObjectByType<ProjectManager>();
            }


            lifetimeCancellation =
                new CancellationTokenSource();
        }


        private void OnEnable()
        {
            if (visualizationManager == null)
            {
                return;
            }


            visualizationManager.VisualizationApplied +=
                HandleApplied;

            visualizationManager.VisualizationCleared +=
                HandleCleared;
        }


        private void OnDisable()
        {
            if (visualizationManager == null)
            {
                return;
            }


            visualizationManager.VisualizationApplied -=
                HandleApplied;

            visualizationManager.VisualizationCleared -=
                HandleCleared;
        }


        private async void Start()
        {
            try
            {
                await LoadOptionsAsync(
                    lifetimeCancellation.Token
                );
            }
            catch (OperationCanceledException)
            {
                // Shutdown.
            }
            catch (Exception exception)
            {
                LastError =
                    $"Visualization catalog: {exception.Message}";

                Debug.LogException(
                    exception,
                    this
                );
            }
            finally
            {
                IsLoaded =
                    true;

                SyncActiveIndex();

                Changed?.Invoke();
            }
        }


        private void OnDestroy()
        {
            lifetimeCancellation?.Cancel();

            lifetimeCancellation?.Dispose();

            lifetimeCancellation =
                null;
        }


        private void Update()
        {
            if (!enableNumberKeys)
            {
                return;
            }


            Keyboard keyboard =
                Keyboard.current;


            if (keyboard == null)
            {
                return;
            }


            if (keyboard.digit0Key.wasPressedThisFrame ||
                keyboard.numpad0Key.wasPressedThisFrame)
            {
                Clear();

                return;
            }


            for (int i = 0; i < 9; i++)
            {
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame ||
                    keyboard[Key.Numpad1 + i].wasPressedThisFrame)
                {
                    Apply(
                        i
                    );

                    return;
                }
            }
        }


        // =========================================================
        // PUBLIC
        // =========================================================

        /// <summary>
        /// Applies the previous (step &lt; 0) or next (step &gt; 0) option,
        /// wrapping around; from "nothing shown" it starts at the first
        /// or last option. Ignored while a view is being applied.
        /// </summary>
        public void Step(
            int step
        )
        {
            if (IsApplying ||
                options.Count == 0 ||
                step == 0)
            {
                return;
            }


            int next =
                ActiveIndex < 0
                    ? (step > 0 ? 0 : options.Count - 1)
                    : ((ActiveIndex + step) % options.Count + options.Count) % options.Count;


            Apply(
                next
            );
        }


        public async void Apply(
            int index
        )
        {
            if (index < 0 ||
                index >= options.Count ||
                visualizationManager == null ||
                !visualizationManager.IsInitialized)
            {
                return;
            }


            int request =
                ++requestCounter;


            PendingIndex =
                index;

            LastError =
                null;


            Changed?.Invoke();


            VisualizationOption option =
                options[index];


            try
            {
                await visualizationManager.ApplyVisualizationAsync(
                    option.Spec
                );


                Debug.Log(
                    $"VisualizationSwitcher: applied " +
                    $"'{option.Id}' ({option.Source}).",
                    this
                );
            }
            catch (OperationCanceledException)
            {
                // Superseded by a newer request or a clear.
            }
            catch (Exception exception)
            {
                if (request == requestCounter)
                {
                    LastError =
                        $"{option.DisplayName}: {exception.Message}";
                }


                Debug.LogException(
                    exception,
                    this
                );
            }
            finally
            {
                if (request == requestCounter)
                {
                    PendingIndex =
                        -1;

                    SyncActiveIndex();

                    Changed?.Invoke();
                }
            }
        }


        public void Clear()
        {
            if (visualizationManager == null ||
                !visualizationManager.IsInitialized)
            {
                return;
            }


            requestCounter++;


            PendingIndex =
                -1;

            LastError =
                null;


            visualizationManager.ClearActiveVisualization();


            SyncActiveIndex();

            Changed?.Invoke();
        }


        // =========================================================
        // LOADING
        // =========================================================

        private async Task LoadOptionsAsync(
            CancellationToken cancellationToken
        )
        {
            options.Clear();


            if (includeStartupVisualization &&
                visualizationManager != null &&
                visualizationManager.StartupVisualization != null &&
                visualizationManager.StartupVisualization.IsConfigured)
            {
                VisualizationSpec startup =
                    visualizationManager.StartupVisualization;


                options.Add(
                    new VisualizationOption
                    {
                        Id =
                            startup.Id,

                        DisplayName =
                            string.IsNullOrWhiteSpace(
                                startup.DisplayName
                            )
                                ? startup.Id
                                : startup.DisplayName,

                        Spec =
                            startup,

                        Source =
                            "scene"
                    }
                );
            }


            if (string.IsNullOrWhiteSpace(catalogPath) ||
                projectManager == null)
            {
                return;
            }


            if (projectManager.LoadTask != null)
            {
                await projectManager.LoadTask;
            }


            if (projectManager.AssetReader == null)
            {
                throw new InvalidOperationException(
                    "ProjectManager has no asset reader."
                );
            }


            string catalogJson;


            try
            {
                catalogJson =
                    await projectManager.AssetReader.ReadTextAsync(
                        catalogPath,
                        cancellationToken
                    );
            }
            catch (Exception exception) when (
                !(exception is OperationCanceledException)
            )
            {
                Debug.LogWarning(
                    $"VisualizationSwitcher: no catalog at " +
                    $"'{catalogPath}' ({exception.Message}). Only " +
                    $"the startup visualization is offered.",
                    this
                );

                return;
            }


            VisualizationCatalogFile catalog =
                JsonUtility.FromJson<VisualizationCatalogFile>(
                    catalogJson
                );


            if (catalog?.visualizations == null)
            {
                throw new InvalidOperationException(
                    $"'{catalogPath}' has no 'visualizations' list."
                );
            }


            var ids =
                new HashSet<string>(
                    StringComparer.Ordinal
                );


            foreach (VisualizationOption option in options)
            {
                ids.Add(
                    option.Id
                );
            }


            foreach (
                VisualizationCatalogEntry entry
                in catalog.visualizations
            )
            {
                cancellationToken.ThrowIfCancellationRequested();


                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.file))
                {
                    throw new InvalidOperationException(
                        $"'{catalogPath}' has an entry without a file."
                    );
                }


                string specPath =
                    RuntimeAssetReader.ResolveSiblingPath(
                        catalogPath,
                        entry.file
                    );


                string specJson =
                    await projectManager.AssetReader.ReadTextAsync(
                        specPath,
                        cancellationToken
                    );


                VisualizationSpec spec =
                    JsonUtility.FromJson<VisualizationSpec>(
                        specJson
                    );


                if (spec == null ||
                    !spec.IsConfigured)
                {
                    throw new InvalidOperationException(
                        $"'{specPath}' is not a configured " +
                        $"VisualizationSpec."
                    );
                }


                string id =
                    string.IsNullOrWhiteSpace(entry.id)
                        ? spec.Id
                        : entry.id;


                if (!ids.Add(id))
                {
                    throw new InvalidOperationException(
                        $"Duplicate visualization id '{id}' in " +
                        $"'{catalogPath}'."
                    );
                }


                options.Add(
                    new VisualizationOption
                    {
                        Id =
                            id,

                        DisplayName =
                            string.IsNullOrWhiteSpace(
                                entry.displayName
                            )
                                ? spec.DisplayName
                                : entry.displayName,

                        Spec =
                            spec,

                        Source =
                            specPath
                    }
                );
            }


            Debug.Log(
                $"VisualizationSwitcher: {options.Count} " +
                $"visualizations available (catalog " +
                $"'{catalogPath}').",
                this
            );
        }


        // =========================================================
        // ACTIVE STATE
        // =========================================================

        private void HandleApplied(
            VisualizationSpec visualization
        )
        {
            SyncActiveIndex();

            Changed?.Invoke();
        }


        private void HandleCleared()
        {
            SyncActiveIndex();

            Changed?.Invoke();
        }


        private void SyncActiveIndex()
        {
            ActiveIndex =
                -1;


            VisualizationSpec active =
                visualizationManager != null
                    ? visualizationManager.ActiveVisualization
                    : null;


            if (active == null)
            {
                return;
            }


            for (int i = 0; i < options.Count; i++)
            {
                if (ReferenceEquals(
                        options[i].Spec,
                        active
                    ))
                {
                    ActiveIndex =
                        i;

                    return;
                }
            }
        }
    }
}
