using MobaTrainer.Core;
using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// Static flat decoration generated at runtime (blob shadow, selection ring, shield bubble).
    /// Keeps generated meshes out of the asset database (meshes appear in Play mode).
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ProceduralFlatMesh : MonoBehaviour
    {
        public enum Shape { Ring, Disc }

        public Shape shape = Shape.Disc;
        public float radius = 0.8f;
        [Tooltip("Ring thickness.")]
        public float thickness = 0.06f;
        [Tooltip("Disc: alpha at the centre / at the edge.")]
        public float centerAlpha = 1f;
        public float edgeAlpha = 0f;
        public float feather = 0.05f;
        public Color color = Color.white;
        public int sortingOrder = 0;

        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void OnEnable() => Apply();

        public void Apply()
        {
            var mf = GetComponent<MeshFilter>();
            var mr = GetComponent<MeshRenderer>();
            mf.sharedMesh = shape == Shape.Ring
                ? MeshFactory.Ring(radius, thickness, feather, 64)
                : MeshFactory.Disc(radius, centerAlpha, edgeAlpha, feather, 48);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sortingOrder = sortingOrder;
            var mpb = new MaterialPropertyBlock();
            mr.GetPropertyBlock(mpb);
            mpb.SetColor(ColorId, color);
            mr.SetPropertyBlock(mpb);
        }
    }
}
