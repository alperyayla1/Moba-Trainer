using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// Cheap world-space health bar: three camera-facing quads (background, fill, shield),
    /// no Canvas per unit. Fill shrinks from the right.
    /// </summary>
    public class HealthBar : MonoBehaviour
    {
        [Header("Parts")]
        public MeshRenderer background;
        public MeshRenderer fill;
        public MeshRenderer shield;

        [Header("Layout")]
        public float width = 1.6f;
        public float barHeight = 0.16f;

        [Header("Colours")]
        public Color backgroundColor = new Color(0f, 0f, 0f, 0.75f);
        public Color fillColor = Color.red;
        public Color shieldColor = new Color(1f, 1f, 1f, 0.85f);

        private static Mesh quad;
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock mpb;
        private Transform cam;

        public static Mesh QuadMesh
        {
            get
            {
                if (quad != null) return quad;
                quad = new Mesh { name = "HealthBarQuad" };
                quad.SetVertices(new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) });
                quad.SetColors(new[] { Color.white, Color.white, Color.white, Color.white });
                quad.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) });
                quad.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
                quad.RecalculateBounds();
                return quad;
            }
        }

        private void Awake()
        {
            mpb = new MaterialPropertyBlock();
            Setup(background, backgroundColor, 0f, 100);
            Setup(fill, fillColor, -0.001f, 101);
            Setup(shield, shieldColor, -0.002f, 102);
            SetValue(1f, 0f);
        }

        private void Setup(MeshRenderer r, Color c, float z, int sortingOrder)
        {
            if (r == null) return;
            r.sortingOrder = sortingOrder;
            var mf = r.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = QuadMesh;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.GetPropertyBlock(mpb);
            mpb.SetColor(ColorId, c);
            r.SetPropertyBlock(mpb);
            r.transform.localPosition = new Vector3(0f, 0f, z);
            r.transform.localScale = new Vector3(width, barHeight, 1f);
        }

        public void SetFillColor(Color c)
        {
            fillColor = c;
            if (fill == null) return;
            fill.GetPropertyBlock(mpb);
            mpb.SetColor(ColorId, c);
            fill.SetPropertyBlock(mpb);
        }

        /// <param name="health01">Health fraction.</param>
        /// <param name="shield01">Shield as a fraction of max health (drawn after the fill).</param>
        public void SetValue(float health01, float shield01)
        {
            health01 = Mathf.Clamp01(health01);
            float inner = width - 0.04f;
            if (fill != null)
            {
                float w = inner * health01;
                fill.transform.localScale = new Vector3(Mathf.Max(0.0001f, w), barHeight - 0.04f, 1f);
                fill.transform.localPosition = new Vector3(-inner * 0.5f + w * 0.5f, 0f, -0.001f);
                fill.enabled = health01 > 0f;
            }
            if (shield != null)
            {
                float s = inner * Mathf.Clamp01(Mathf.Min(shield01, 1f - health01 + shield01));
                s = Mathf.Min(s, inner);
                float start = Mathf.Min(inner * health01, inner - s);
                shield.transform.localScale = new Vector3(Mathf.Max(0.0001f, s), barHeight - 0.04f, 1f);
                shield.transform.localPosition = new Vector3(-inner * 0.5f + start + s * 0.5f, 0f, -0.002f);
                shield.enabled = shield01 > 0f;
            }
        }

        private void LateUpdate()
        {
            if (cam == null)
            {
                Camera main = Camera.main;
                if (main == null) return;
                cam = main.transform;
            }
            transform.rotation = cam.rotation;
        }
    }
}
