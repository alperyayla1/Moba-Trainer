using MobaTrainer.Core;
using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// Ground preview while aiming: a range ring around the caster plus the ability's shape
    /// (line, circle, blink/dash destination). Also used for the attack-range ring during attack-move.
    /// </summary>
    public class CastIndicator : MonoBehaviour
    {
        [Header("Markers")]
        public GroundMarker rangeRing;
        public GroundMarker shape;
        public GroundMarker destination;
        public GroundMarker attackRangeRing;

        [Header("Colours")]
        public Color rangeColor = new Color(1f, 1f, 1f, 0.28f);
        public Color shapeColor = Palette.Accent.WithAlpha(0.42f);
        public Color attackRangeColor = Palette.Danger.WithAlpha(0.55f);

        private Mesh unitRing;
        private Mesh unitDisc;
        private Mesh destRing;
        private Mesh lineMesh;
        private float lineLength = -1f;
        private float lineWidth = -1f;

        private void Awake()
        {
            unitRing = MeshFactory.Ring(1f, 0.035f, 0.02f, 128);
            unitDisc = MeshFactory.Disc(1f, 0.6f, 1f, 0.03f, 64);
            destRing = MeshFactory.Ring(0.5f, 0.07f, 0.03f, 48);
            HideAbility();
            HideAttackRange();
        }

        public void HideAbility()
        {
            if (rangeRing != null) rangeRing.gameObject.SetActive(false);
            if (shape != null) shape.gameObject.SetActive(false);
            if (destination != null) destination.gameObject.SetActive(false);
        }

        public void HideAttackRange()
        {
            if (attackRangeRing != null) attackRangeRing.gameObject.SetActive(false);
        }

        public void ShowAttackRange(Vector3 casterPos, float edgeRange)
        {
            if (attackRangeRing == null) return;
            attackRangeRing.gameObject.SetActive(true);
            attackRangeRing.Set(unitRing, casterPos, Quaternion.identity, new Vector3(edgeRange, 1f, edgeRange), attackRangeColor, 0.04f);
        }

        /// <summary>Draws the preview for <paramref name="ability"/> aimed at <paramref name="cursor"/>.</summary>
        public void ShowAbility(AbilityDefinition ability, Vector3 casterPos, Vector3 cursor)
        {
            Vector3 dir = cursor - casterPos;
            dir.y = 0f;
            float dist = dir.magnitude;
            Vector3 dirN = dist > 0.001f ? dir / dist : Vector3.forward;
            Quaternion rot = Quaternion.LookRotation(dirN, Vector3.up);

            rangeRing.gameObject.SetActive(true);
            rangeRing.Set(unitRing, casterPos, Quaternion.identity, new Vector3(ability.range, 1f, ability.range), rangeColor, 0.035f);

            switch (ability.shape)
            {
                case AbilityShape.LinearSkillshot:
                {
                    if (!Mathf.Approximately(lineLength, ability.range) || !Mathf.Approximately(lineWidth, ability.size))
                    {
                        lineLength = ability.range;
                        lineWidth = ability.size;
                        lineMesh = MeshFactory.Rect(lineLength, lineWidth, 0.05f);
                    }
                    shape.gameObject.SetActive(true);
                    shape.Set(lineMesh, casterPos, rot, Vector3.one, shapeColor, 0.03f);
                    destination.gameObject.SetActive(false);
                    break;
                }
                case AbilityShape.CircleSkillshot:
                {
                    Vector3 center = casterPos + dirN * Mathf.Min(dist, ability.range);
                    shape.gameObject.SetActive(true);
                    shape.Set(unitDisc, center, Quaternion.identity, new Vector3(ability.size, 1f, ability.size), shapeColor, 0.03f);
                    destination.gameObject.SetActive(false);
                    break;
                }
                case AbilityShape.Blink:
                case AbilityShape.ShiftBolt:
                case AbilityShape.Dash:
                {
                    float reach = ability.shape == AbilityShape.Dash ? ability.range : Mathf.Min(dist, ability.range);
                    Vector3 dest = Arena.ClampStatic(casterPos + dirN * reach, 0.65f);
                    shape.gameObject.SetActive(false);
                    destination.gameObject.SetActive(true);
                    destination.Set(destRing, dest, Quaternion.identity, Vector3.one * 1.4f, shapeColor.WithAlpha(0.9f), 0.035f);
                    break;
                }
                default:
                    HideAbility();
                    break;
            }
        }
    }
}
