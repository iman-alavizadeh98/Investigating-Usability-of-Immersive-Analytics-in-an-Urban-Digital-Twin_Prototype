using UnityEngine;

namespace UrbanAnalytics.Visualization
{
    /// <summary>
    /// Destroys a runtime-generated mesh together with the object that
    /// shows it (e.g. area outlines), so removing a visualization does not
    /// leak meshes.
    /// </summary>
    public sealed class OwnedMesh :
        MonoBehaviour
    {
        public Mesh Mesh;


        private void OnDestroy()
        {
            if (Mesh != null)
            {
                Destroy(Mesh);
            }
        }
    }
}
