using System;
using System.Collections.Generic;
using UnityEngine;

namespace UrbanAnalytics.Visualization
{
    /// <summary>
    /// Runtime result produced by one visualization renderer.
    ///
    /// The specification describes what should exist.
    /// The instance represents what currently exists in Unity.
    /// </summary>
    public sealed class VisualizationLayerInstance :
        IDisposable
    {
        private readonly Action cleanup;

        private bool disposed;


        public string LayerId
        {
            get;
        }

        public VisualizationMark Mark
        {
            get;
        }

        public GameObject Root
        {
            get;
        }

        public IReadOnlyList<VisualizationLegendInfo>
            Legends
        {
            get;
        }


        public VisualizationLayerInstance(
            string layerId,
            VisualizationMark mark,
            GameObject root,
            IReadOnlyList<VisualizationLegendInfo> legends,
            Action cleanup = null
        )
        {
            LayerId =
                layerId;

            Mark =
                mark;

            Root =
                root;

            Legends =
                legends
                ?? Array.Empty<VisualizationLegendInfo>();

            this.cleanup =
                cleanup;
        }


        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed =
                true;


            cleanup?.Invoke();


            if (Root != null)
            {
                UnityEngine.Object.Destroy(
                    Root
                );
            }
        }
    }
}