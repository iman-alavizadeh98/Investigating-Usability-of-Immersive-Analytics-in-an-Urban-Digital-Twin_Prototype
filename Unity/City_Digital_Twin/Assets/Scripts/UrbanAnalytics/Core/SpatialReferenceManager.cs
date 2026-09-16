using UnityEngine;

namespace UrbanAnalytics.Core
{
    public class SpatialReferenceManager : MonoBehaviour
    {
        [SerializeField]
        private ProjectManager projectManager;


        private ProjectManifest Manifest =>
            projectManager.Manifest;


        public bool IsReady =>
            projectManager != null &&
            projectManager.IsLoaded;


        private void Awake()
        {
            if (projectManager == null)
            {
                projectManager =
                    FindFirstObjectByType<ProjectManager>();
            }


            if (projectManager == null)
            {
                Debug.LogError(
                    "SpatialReferenceManager could not find ProjectManager."
                );
            }
        }


        // ========================================================
        // SOURCE CRS -> UNITY
        // ========================================================

        public Vector3 ToUnity(
            double easting,
            double northing,
            double elevation = 0.0
        )
        {
            if (!IsReady)
            {
                Debug.LogError(
                    "SpatialReferenceManager is not ready."
                );

                return Vector3.zero;
            }


            SourceCoordinate origin =
                Manifest
                .unityTransform
                .originInSourceCRS;


            double localEasting =
                easting - origin.easting;


            double localNorthing =
                northing - origin.northing;


            double localElevation =
                elevation - origin.elevation;


            double scale =
                Manifest
                .unityTransform
                .metersToUnity;


            float x =
                (float)(
                    localEasting * scale
                );


            float y =
                (float)(
                    localElevation * scale
                );


            float z =
                (float)(
                    localNorthing * scale
                );


            return new Vector3(
                x,
                y,
                z
            );
        }


        // ========================================================
        // UNITY -> SOURCE CRS
        // ========================================================

        public SourceCoordinate ToSourceCRS(
            Vector3 unityPosition
        )
        {
            if (!IsReady)
            {
                Debug.LogError(
                    "SpatialReferenceManager is not ready."
                );

                return null;
            }


            SourceCoordinate origin =
                Manifest
                .unityTransform
                .originInSourceCRS;


            double scale =
                Manifest
                .unityTransform
                .metersToUnity;


            return new SourceCoordinate
            {
                easting =
                    origin.easting
                    + unityPosition.x / scale,

                northing =
                    origin.northing
                    + unityPosition.z / scale,

                elevation =
                    origin.elevation
                    + unityPosition.y / scale
            };
        }
    }
}