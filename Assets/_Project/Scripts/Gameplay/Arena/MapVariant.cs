using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// An alternative look and layout for the arena (Extreme Survivor's "Ember Wastes"): its own props
    /// and blockers, darker lighting and fog. Built by the scene builder, switched on only while that mode
    /// runs and fully restored afterwards, so the other drills keep the normal map.
    /// </summary>
    public class MapVariant : MonoBehaviour
    {
        [Header("Content")]
        public GameObject root;
        [Tooltip("Normal-map objects hidden while this map is shown (walls, grass, trees, motes).")]
        public GameObject[] hide;
        [Tooltip("Blocking circles of this map (x, z, -, radius).")]
        public Vector4[] circles = new Vector4[0];

        [Header("Lighting")]
        public Light sun;
        public Color sunColor = Color.white;
        public float sunIntensity = 1f;
        public Color ambientSky = Color.gray, ambientEquator = Color.gray, ambientGround = Color.gray;
        public Color fogColor = Color.black;
        public float fogDensity = 0.01f;
        public Color cameraBackground = Color.black;

        public bool Active { get; private set; }

        // What we replaced (restored on Apply(false)).
        private Vector4[] savedCircles;
        private Color savedSunColor, savedSky, savedEquator, savedGround, savedFog, savedBg;
        private float savedSunIntensity, savedFogDensity;

        public void Apply(bool on)
        {
            if (on == Active) return;
            Active = on;
            ObstacleField field = ObstacleField.Instance;
            Camera cam = Camera.main;
            if (on)
            {
                if (field != null) { savedCircles = field.circles; field.SetCircles(circles); }
                if (sun != null) { savedSunColor = sun.color; savedSunIntensity = sun.intensity; sun.color = sunColor; sun.intensity = sunIntensity; }
                savedSky = RenderSettings.ambientSkyColor; savedEquator = RenderSettings.ambientEquatorColor; savedGround = RenderSettings.ambientGroundColor;
                savedFog = RenderSettings.fogColor; savedFogDensity = RenderSettings.fogDensity;
                RenderSettings.ambientSkyColor = ambientSky; RenderSettings.ambientEquatorColor = ambientEquator; RenderSettings.ambientGroundColor = ambientGround;
                RenderSettings.fogColor = fogColor; RenderSettings.fogDensity = fogDensity;
                if (cam != null) { savedBg = cam.backgroundColor; cam.backgroundColor = cameraBackground; }
            }
            else
            {
                if (field != null && savedCircles != null) field.SetCircles(savedCircles);
                if (sun != null) { sun.color = savedSunColor; sun.intensity = savedSunIntensity; }
                RenderSettings.ambientSkyColor = savedSky; RenderSettings.ambientEquatorColor = savedEquator; RenderSettings.ambientGroundColor = savedGround;
                RenderSettings.fogColor = savedFog; RenderSettings.fogDensity = savedFogDensity;
                if (cam != null) cam.backgroundColor = savedBg;
            }
            if (root != null) root.SetActive(on);
            if (hide != null) foreach (GameObject g in hide) if (g != null) g.SetActive(!on);
        }

        private void OnDisable()
        {
            if (Active) Apply(false);
        }
    }
}
