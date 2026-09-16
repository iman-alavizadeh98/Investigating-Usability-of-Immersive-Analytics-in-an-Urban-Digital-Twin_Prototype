using System;

namespace UrbanAnalytics.Core
{
    [Serializable]
    public class ProjectManifest
    {
        public string packageVersion;
        public string projectId;
        public string displayName;

        public SpatialReferenceDefinition spatialReference;
        public UnityTransformDefinition unityTransform;

        public ResourceReference[] urbanContext;
        public ResourceReference[] spatialLayers;
        public ResourceReference[] dataLayers;
        public ResourceReference[] associations;
    }


    [Serializable]
    public class SpatialReferenceDefinition
    {
        public string sourceCRS;
        public string sourceUnit;
    }


    [Serializable]
    public class UnityTransformDefinition
    {
        public SourceCoordinate originInSourceCRS;

        public float metersToUnity;

        public AxisMapping axisMapping;
    }


    [Serializable]
    public class SourceCoordinate
    {
        public double easting;
        public double northing;
        public double elevation;
    }


    [Serializable]
    public class AxisMapping
    {
        public string easting;
        public string northing;
        public string elevation;
    }


    [Serializable]
    public class ResourceReference
    {
        public string id;

        // We use one generic path field.
        // It may point to another manifest/definition later.
        public string definition;
    }
}