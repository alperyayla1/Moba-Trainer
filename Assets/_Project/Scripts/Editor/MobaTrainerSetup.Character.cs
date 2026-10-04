using System.Collections.Generic;
using MobaTrainer.Core;
using MobaTrainer.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MobaTrainer.EditorTools
{
    /// <summary>
    /// Original low-poly ranged hero ("Wayfarer"): a young adventurer in a long travel coat with a scarf,
    /// satchel and a glowing focus stone in the right hand. Built from primitives on a simple joint
    /// hierarchy that <see cref="CharacterAnimator"/> drives procedurally. Same rig for player and
    /// sparring partners, only the colours differ.
    /// </summary>
    public static partial class MobaTrainerSetup
    {
        /// <summary>Visual size of hero models (gameplay radius is unchanged).</summary>
        public const float CharacterScale = 1.3f;

        internal class CharacterLook
        {
            public string id;
            public int coat, coatDark, scarf, hair, focus;
        }

        internal static readonly CharacterLook PlayerLook = new CharacterLook
        {
            id = "Player", coat = 0x2F5C9C, coatDark = 0x22416E, scarf = 0xD8B25A, hair = 0x6A4A2C, focus = 0x7FE6FF,
        };

        internal static readonly CharacterLook EnemyLook = new CharacterLook
        {
            id = "Enemy", coat = 0x9C3530, coatDark = 0x6B2522, scarf = 0x2B2B30, hair = 0x2A211C, focus = 0xFF9A4A,
        };

        internal class CharacterBuild
        {
            public Transform model;
            public CharacterAnimator animator;
            public Renderer[] flashRenderers;
        }

        private static CharacterBuild BuildCharacter(Transform parent, CharacterLook look, bool autoFace)
        {
            Shader lit = FindShader("Universal Render Pipeline/Lit");
            Material skin = LitMat("CharSkin", lit, 0xE7B894, 0.25f);
            Material pants = LitMat("CharPants", lit, 0x3A3E48, 0.15f);
            Material leather = LitMat("CharLeather", lit, 0x5A3E28, 0.3f);
            Material coat = LitMat("CharCoat" + look.id, lit, look.coat, 0.2f);
            Material coatDark = LitMat("CharCoatDark" + look.id, lit, look.coatDark, 0.2f);
            Material scarf = LitMat("CharScarf" + look.id, lit, look.scarf, 0.2f);
            Material hair = LitMat("CharHair" + look.id, lit, look.hair, 0.1f);
            Material focus = LitMat("CharFocus" + look.id, lit, look.focus, 0.8f);
            focus.EnableKeyword("_EMISSION");
            focus.SetColor("_EmissionColor", Palette.Hex(look.focus) * 2.2f);
            focus.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            foreach (Material m in new[] { skin, pants, leather, coat, coatDark, scarf, hair, focus }) EditorUtility.SetDirty(m);

            var renderers = new List<Renderer>();
            GameObject Part(PrimitiveType type, string name, Transform p, Material mat, Vector3 pos, Vector3 scale, Vector3 euler = default, bool flash = true)
            {
                GameObject g = NewPrimitive(type, name, p, mat);
                g.transform.localPosition = pos;
                g.transform.localScale = scale;
                g.transform.localRotation = Quaternion.Euler(euler);
                if (flash) renderers.Add(g.GetComponent<MeshRenderer>());
                return g;
            }
            Transform Joint(string name, Transform p, Vector3 pos)
            {
                var j = new GameObject(name).transform;
                j.SetParent(p, false);
                j.localPosition = pos;
                return j;
            }

            Transform model = Joint("Model", parent, Vector3.zero);
            model.localScale = Vector3.one * CharacterScale; // champions read bigger than their hitbox
            Transform rig = Joint("Rig", model, Vector3.zero);
            Transform hips = Joint("Hips", rig, new Vector3(0f, 0.92f, 0f));
            Part(PrimitiveType.Cube, "Pelvis", hips, pants, new Vector3(0f, 0f, 0f), new Vector3(0.34f, 0.16f, 0.22f));

            Transform[] Leg(string side, float x)
            {
                Transform leg = Joint("Leg" + side, hips, new Vector3(x, -0.03f, 0f));
                Part(PrimitiveType.Capsule, "Thigh", leg, pants, new Vector3(0f, -0.21f, 0f), new Vector3(0.15f, 0.22f, 0.15f));
                Transform knee = Joint("Knee" + side, leg, new Vector3(0f, -0.42f, 0f));
                Part(PrimitiveType.Capsule, "Shin", knee, pants, new Vector3(0f, -0.2f, 0f), new Vector3(0.13f, 0.2f, 0.13f));
                Part(PrimitiveType.Cube, "Boot", knee, leather, new Vector3(0f, -0.43f, 0.04f), new Vector3(0.15f, 0.11f, 0.25f));
                return new[] { leg, knee };
            }
            Transform[] legL = Leg("L", -0.1f);
            Transform[] legR = Leg("R", 0.1f);

            Transform spine = Joint("Spine", hips, new Vector3(0f, 0.06f, 0f));
            Part(PrimitiveType.Cube, "Torso", spine, coat, new Vector3(0f, 0.27f, 0f), new Vector3(0.38f, 0.46f, 0.24f));
            Part(PrimitiveType.Cube, "CoatTail", spine, coatDark, new Vector3(0f, -0.14f, -0.02f), new Vector3(0.42f, 0.34f, 0.27f), new Vector3(-6f, 0f, 0f));
            Part(PrimitiveType.Cube, "Belt", spine, leather, new Vector3(0f, 0.04f, 0f), new Vector3(0.41f, 0.06f, 0.27f));
            Part(PrimitiveType.Cube, "Satchel", spine, leather, new Vector3(-0.23f, -0.02f, 0.02f), new Vector3(0.09f, 0.16f, 0.2f));
            Part(PrimitiveType.Cube, "Strap", spine, leather, new Vector3(0f, 0.3f, 0.125f), new Vector3(0.05f, 0.55f, 0.02f), new Vector3(0f, 0f, 38f));
            Part(PrimitiveType.Cube, "Scarf", spine, scarf, new Vector3(0f, 0.52f, 0.01f), new Vector3(0.3f, 0.09f, 0.28f));
            Part(PrimitiveType.Cube, "ScarfTail", spine, scarf, new Vector3(0.1f, 0.38f, -0.15f), new Vector3(0.09f, 0.26f, 0.04f), new Vector3(12f, 0f, -8f));

            Transform neck = Joint("Neck", spine, new Vector3(0f, 0.54f, 0f));
            Part(PrimitiveType.Sphere, "Head", neck, skin, new Vector3(0f, 0.15f, 0f), new Vector3(0.25f, 0.27f, 0.25f));
            Part(PrimitiveType.Sphere, "Hair", neck, hair, new Vector3(0f, 0.21f, -0.02f), new Vector3(0.28f, 0.2f, 0.29f));
            Part(PrimitiveType.Cube, "Fringe", neck, hair, new Vector3(0.03f, 0.25f, 0.1f), new Vector3(0.22f, 0.07f, 0.08f), new Vector3(-20f, 0f, 8f));
            Part(PrimitiveType.Cube, "Tuft", neck, hair, new Vector3(-0.02f, 0.31f, -0.04f), new Vector3(0.07f, 0.1f, 0.12f), new Vector3(-25f, 10f, 0f));

            Transform[] Arm(string side, float x)
            {
                float sign = Mathf.Sign(x);
                Transform arm = Joint("Arm" + side, spine, new Vector3(x, 0.47f, 0f));
                Part(PrimitiveType.Cube, "Shoulder", arm, coatDark, new Vector3(0f, 0.02f, 0f), new Vector3(0.15f, 0.08f, 0.17f), new Vector3(0f, 0f, -12f * sign));
                Part(PrimitiveType.Capsule, "Upper", arm, coat, new Vector3(0f, -0.14f, 0f), new Vector3(0.12f, 0.15f, 0.12f));
                Transform elbow = Joint("Elbow" + side, arm, new Vector3(0f, -0.29f, 0f));
                Part(PrimitiveType.Capsule, "Fore", elbow, coat, new Vector3(0f, -0.13f, 0f), new Vector3(0.11f, 0.14f, 0.11f));
                Part(PrimitiveType.Cube, "Cuff", elbow, leather, new Vector3(0f, -0.21f, 0f), new Vector3(0.12f, 0.06f, 0.12f));
                Part(PrimitiveType.Sphere, "Hand", elbow, skin, new Vector3(0f, -0.3f, 0f), new Vector3(0.1f, 0.11f, 0.1f));
                return new[] { arm, elbow };
            }
            Transform[] armL = Arm("L", -0.26f);
            Transform[] armR = Arm("R", 0.26f);

            // Focus stone in the right hand (not flashed: it already glows).
            GameObject stone = Part(PrimitiveType.Sphere, "Focus", armR[1], focus, new Vector3(0f, -0.36f, 0.04f), Vector3.one * 0.11f, default, false);
            stone.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            var anim = model.gameObject.AddComponent<CharacterAnimator>();
            anim.rigRoot = rig;
            anim.hips = hips;
            anim.spine = spine;
            anim.neck = neck;
            anim.legL = legL[0]; anim.kneeL = legL[1];
            anim.legR = legR[0]; anim.kneeR = legR[1];
            anim.armL = armL[0]; anim.elbowL = armL[1];
            anim.armR = armR[0]; anim.elbowR = armR[1];
            anim.autoFace = autoFace;

            return new CharacterBuild { model = model, animator = anim, flashRenderers = renderers.ToArray() };
        }
    }
}
