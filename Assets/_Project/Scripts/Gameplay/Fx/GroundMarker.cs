using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// A pooled flat mesh on the ground (ring, disc, rectangle, sector) tinted through a
    /// MaterialPropertyBlock. Used by indicators, telegraphs and short-lived effects.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class GroundMarker : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int FillId = Shader.PropertyToID("_Fill");

        private MeshFilter filter;
        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock mpb;
        private Color color = Color.white;
        private float fill = 1f;

        public Color Color => color;

        private void Awake()
        {
            filter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            mpb = new MaterialPropertyBlock();
        }

        public void Set(Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale, Color tint, float heightOffset = 0.02f)
        {
            if (filter == null) Awake();
            filter.sharedMesh = mesh;
            position.y = heightOffset;
            transform.SetPositionAndRotation(position, rotation);
            transform.localScale = scale;
            fill = 1f;
            SetColor(tint);
        }

        public void SetMaterial(Material material)
        {
            if (meshRenderer == null) Awake();
            meshRenderer.sharedMaterial = material;
        }

        public void SetSortingOrder(int order)
        {
            if (meshRenderer == null) Awake();
            meshRenderer.sortingOrder = order;
        }

        public void SetColor(Color tint)
        {
            color = tint;
            Apply();
        }

        /// <summary>0..1 fill progress for telegraph materials (ignored by plain unlit).</summary>
        public void SetFill(float value)
        {
            fill = value;
            Apply();
        }

        private void Apply()
        {
            if (meshRenderer == null) Awake();
            meshRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(ColorId, color);
            mpb.SetFloat(FillId, fill);
            meshRenderer.SetPropertyBlock(mpb);
        }
    }
}
