using System;
using System.Threading.Tasks;
using UnityEngine;
using UrbanAnalytics.Core.IO;

namespace UrbanAnalytics.Core
{
    public class ProjectManager : MonoBehaviour
    {
        [Header("Project")]
        [SerializeField]
        private string manifestFileName =
            "project_manifest.json";


        public ProjectManifest Manifest
        {
            get;
            private set;
        }


        public RuntimeAssetReader AssetReader
        {
            get;
            private set;
        }


        public bool IsLoaded
        {
            get;
            private set;
        }


        public bool IsLoading
        {
            get;
            private set;
        }


        public string LastError
        {
            get;
            private set;
        }


        public Task LoadTask
        {
            get;
            private set;
        }


        private void Awake()
        {
            AssetReader =
                new RuntimeAssetReader();

            LoadTask =
                LoadProjectAsync();
        }


        private async Task LoadProjectAsync()
        {
            IsLoading = true;
            IsLoaded = false;
            LastError = null;


            try
            {
                string json =
                    await AssetReader.ReadTextAsync(
                        manifestFileName
                    );


                if (string.IsNullOrWhiteSpace(
                        json
                    ))
                {
                    throw new InvalidOperationException(
                        "Project manifest is empty."
                    );
                }


                Manifest =
                    JsonUtility.FromJson<ProjectManifest>(
                        json
                    );


                if (Manifest == null)
                {
                    throw new InvalidOperationException(
                        "Failed to deserialize project manifest."
                    );
                }


                ValidateManifest(
                    Manifest
                );


                IsLoaded = true;


                Debug.Log(
                    $"Project loaded:\n" +
                    $"ID: {Manifest.projectId}\n" +
                    $"Name: {Manifest.displayName}\n" +
                    $"Package Version: {Manifest.packageVersion}\n" +
                    $"CRS: {Manifest.spatialReference.sourceCRS}\n" +
                    $"Meters To Unity: " +
                    $"{Manifest.unityTransform.metersToUnity}"
                );
            }
            catch (Exception exception)
            {
                Manifest = null;
                IsLoaded = false;

                LastError =
                    exception.Message;


                Debug.LogException(
                    exception,
                    this
                );
            }
            finally
            {
                IsLoading = false;
            }
        }


        private static void ValidateManifest(
            ProjectManifest manifest
        )
        {
            if (string.IsNullOrWhiteSpace(
                    manifest.packageVersion
                ))
            {
                throw new InvalidOperationException(
                    "Project manifest is missing packageVersion."
                );
            }


            if (string.IsNullOrWhiteSpace(
                    manifest.projectId
                ))
            {
                throw new InvalidOperationException(
                    "Project manifest is missing projectId."
                );
            }


            if (string.IsNullOrWhiteSpace(
                    manifest.displayName
                ))
            {
                throw new InvalidOperationException(
                    "Project manifest is missing displayName."
                );
            }


            if (manifest.spatialReference == null)
            {
                throw new InvalidOperationException(
                    "Project manifest is missing spatialReference."
                );
            }


            if (string.IsNullOrWhiteSpace(
                    manifest.spatialReference.sourceCRS
                ))
            {
                throw new InvalidOperationException(
                    "Project manifest is missing sourceCRS."
                );
            }


            if (string.IsNullOrWhiteSpace(
                    manifest.spatialReference.sourceUnit
                ))
            {
                throw new InvalidOperationException(
                    "Project manifest is missing sourceUnit."
                );
            }


            if (manifest.unityTransform == null)
            {
                throw new InvalidOperationException(
                    "Project manifest is missing unityTransform."
                );
            }


            if (manifest.unityTransform.originInSourceCRS == null)
            {
                throw new InvalidOperationException(
                    "Project manifest is missing " +
                    "unityTransform.originInSourceCRS."
                );
            }


            if (manifest.unityTransform.metersToUnity <= 0.0f)
            {
                throw new InvalidOperationException(
                    "metersToUnity must be greater than zero."
                );
            }


            if (manifest.unityTransform.axisMapping == null)
            {
                throw new InvalidOperationException(
                    "Project manifest is missing axisMapping."
                );
            }
        }
    }
}