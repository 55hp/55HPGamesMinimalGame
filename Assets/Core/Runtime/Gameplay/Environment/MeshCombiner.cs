using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace hp55games.Mobile.Core.Gameplay.Environment
{
    /// <summary>
    /// On Awake, combines all child MeshFilters into a single mesh on the root GameObject,
    /// then disables only the MeshRenderers that were baked so only the merged draw-call remains.
    ///
    /// Attach this to the root of a prefab whose children are static sub-meshes.
    /// The root must already have a MeshFilter and MeshRenderer (enforced by RequireComponent).
    /// The material of the first valid child MeshRenderer is assigned to the root.
    ///
    /// Only MeshRenderers are disabled — GameObjects are never deactivated, so colliders,
    /// triggers, lights and other non-mesh children keep working normally.
    ///
    /// Vertex matrices are remapped from each child's local space to the root's local space,
    /// so the combined mesh is correct regardless of child transforms or root scale.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [DisallowMultipleComponent]
    public class MeshCombiner : MonoBehaviour
    {
        private void Awake()
        {
            MeshFilter   rootFilter   = GetComponent<MeshFilter>();
            MeshRenderer rootRenderer = GetComponent<MeshRenderer>();

            MeshFilter[] allFilters = GetComponentsInChildren<MeshFilter>(includeInactive: true);

            var      combines     = new List<CombineInstance>(allFilters.Length);
            Material firstMaterial = null;

            Matrix4x4 rootWorldToLocal = transform.worldToLocalMatrix;

            foreach (MeshFilter mf in allFilters)
            {
                // Skip the root's own MeshFilter
                if (mf == rootFilter) continue;
                if (mf.sharedMesh == null) continue;

                // Grab material from the first child that has one
                if (firstMaterial == null)
                {
                    MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                    if (mr != null && mr.sharedMaterial != null)
                        firstMaterial = mr.sharedMaterial;
                }

                combines.Add(new CombineInstance
                {
                    mesh = mf.sharedMesh,
                    // Transform vertices from child local space → root local space
                    transform = rootWorldToLocal * mf.transform.localToWorldMatrix
                });
            }

            if (combines.Count == 0)
            {
                Debug.LogWarning($"[MeshCombiner] '{name}': no valid child MeshFilters found. Skipping.", this);
                return;
            }

            // Use 32-bit indices to support meshes with more than 65 535 vertices
            Mesh combined = new Mesh
            {
                name        = $"{name}_combined",
                indexFormat = IndexFormat.UInt32
            };

            combined.CombineMeshes(combines.ToArray(), mergeSubMeshes: true, useMatrices: true);
            combined.RecalculateBounds();

            rootFilter.sharedMesh        = combined;
            rootRenderer.sharedMaterial  = firstMaterial;
            rootRenderer.enabled         = true;

            // Disable only the MeshRenderers that were baked into the combined mesh.
            // We never touch the GameObject active state so colliders, triggers,
            // lights and other non-mesh children keep working normally.
            foreach (MeshFilter mf in allFilters)
            {
                if (mf == rootFilter) continue;
                if (mf.sharedMesh == null) continue;

                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = false;
            }
        }
    }
}
