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
            public bool melee;
            public bool fire;
        }

        internal static readonly CharacterLook PlayerLook = new CharacterLook
        {
            id = "Player", coat = 0x2F5C9C, coatDark = 0x22416E, scarf = 0xD8B25A, hair = 0x6A4A2C, focus = 0x7FE6FF,
        };

        internal static readonly CharacterLook EnemyLook = new CharacterLook
        {
            id = "Enemy", coat = 0x9C3530, coatDark = 0x6B2522, scarf = 0x2B2B30, hair = 0x2A211C, focus = 0xFF9A4A,
        };

        internal static readonly CharacterLook EnemyBlueLook = new CharacterLook
        {
            id = "EnemyBlue", coat = 0x1D7F8E, coatDark = 0x135560, scarf = 0x1B1F24, hair = 0xD9D4C8, focus = 0x8FF2FF,
        };

        internal static readonly CharacterLook EnemyMeleeLook = new CharacterLook
        {
            id = "EnemyMelee", coat = 0x4B4F58, coatDark = 0x2E3138, scarf = 0xE07A1F, hair = 0x1A1512, focus = 0xFFB060, melee = true,
        };

        internal static readonly CharacterLook EnemyFireLook = new CharacterLook
        {
            // Charred coat, glowing ember scarf and burning hair.
            id = "EnemyFire", coat = 0x2E1E18, coatDark = 0x1A110D, scarf = 0xFF5A14, hair = 0xFF7A1A, focus = 0xFFB040, fire = true,
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
            if (look.fire)
            {
                // Burning hair and ember scarf glow.
                foreach (Material g in new[] { hair, scarf })
                {
                    g.EnableKeyword("_EMISSION");
                    g.SetColor("_EmissionColor", g.GetColor("_BaseColor") * 1.6f);
                    g.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
            }
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
            Part(PrimitiveType.Sphere, "Pelvis", hips, pants, new Vector3(0f, 0f, 0f), new Vector3(0.34f, 0.2f, 0.24f));

            Transform[] Leg(string side, float x)
            {
                Transform leg = Joint("Leg" + side, hips, new Vector3(x, -0.03f, 0f));
                Part(PrimitiveType.Capsule, "Thigh", leg, pants, new Vector3(0f, -0.21f, 0f), new Vector3(0.15f, 0.22f, 0.15f));
                Transform knee = Joint("Knee" + side, leg, new Vector3(0f, -0.42f, 0f));
                Part(PrimitiveType.Capsule, "Shin", knee, pants, new Vector3(0f, -0.2f, 0f), new Vector3(0.13f, 0.2f, 0.13f));
                Part(PrimitiveType.Capsule, "Boot", knee, leather, new Vector3(0f, -0.43f, 0.04f), new Vector3(0.15f, 0.13f, 0.13f), new Vector3(90f, 0f, 0f));
                return new[] { leg, knee };
            }
            Transform[] legL = Leg("L", -0.1f);
            Transform[] legR = Leg("R", 0.1f);

            Transform spine = Joint("Spine", hips, new Vector3(0f, 0.06f, 0f));
            Part(PrimitiveType.Capsule, "Torso", spine, coat, new Vector3(0f, 0.27f, 0f), new Vector3(0.40f, 0.27f, 0.27f));
            Part(PrimitiveType.Cylinder, "CoatTail", spine, coatDark, new Vector3(0f, -0.13f, -0.02f), new Vector3(0.43f, 0.16f, 0.31f), new Vector3(-6f, 0f, 0f));
            Part(PrimitiveType.Cylinder, "Belt", spine, leather, new Vector3(0f, 0.04f, 0f), new Vector3(0.42f, 0.03f, 0.29f));
            Part(PrimitiveType.Sphere, "Satchel", spine, leather, new Vector3(-0.23f, -0.02f, 0.02f), new Vector3(0.11f, 0.17f, 0.2f));
            Part(PrimitiveType.Cube, "Strap", spine, leather, new Vector3(0f, 0.3f, 0.125f), new Vector3(0.05f, 0.55f, 0.02f), new Vector3(0f, 0f, 38f));
            Part(PrimitiveType.Cylinder, "Scarf", spine, scarf, new Vector3(0f, 0.52f, 0.01f), new Vector3(0.3f, 0.05f, 0.28f));
            Part(PrimitiveType.Capsule, "ScarfTail", spine, scarf, new Vector3(0.1f, 0.38f, -0.15f), new Vector3(0.09f, 0.13f, 0.05f), new Vector3(12f, 0f, -8f));

            Transform neck = Joint("Neck", spine, new Vector3(0f, 0.54f, 0f));
            Part(PrimitiveType.Sphere, "Head", neck, skin, new Vector3(0f, 0.15f, 0f), new Vector3(0.25f, 0.27f, 0.25f));
            Part(PrimitiveType.Sphere, "Hair", neck, hair, new Vector3(0f, 0.21f, -0.02f), new Vector3(0.28f, 0.2f, 0.29f));
            Part(PrimitiveType.Sphere, "Fringe", neck, hair, new Vector3(0.03f, 0.25f, 0.09f), new Vector3(0.24f, 0.08f, 0.12f), new Vector3(-20f, 0f, 8f));
            Part(PrimitiveType.Sphere, "EyeL", neck, hair, new Vector3(-0.05f, 0.16f, 0.115f), Vector3.one * 0.035f);
            Part(PrimitiveType.Sphere, "EyeR", neck, hair, new Vector3(0.05f, 0.16f, 0.115f), Vector3.one * 0.035f);
            Part(PrimitiveType.Sphere, "Tuft", neck, hair, new Vector3(-0.02f, 0.31f, -0.04f), new Vector3(0.09f, 0.12f, 0.13f), new Vector3(-25f, 10f, 0f));

            Transform[] Arm(string side, float x)
            {
                float sign = Mathf.Sign(x);
                Transform arm = Joint("Arm" + side, spine, new Vector3(x, 0.47f, 0f));
                Part(PrimitiveType.Sphere, "Shoulder", arm, coatDark, new Vector3(0f, 0.02f, 0f), new Vector3(0.17f, 0.12f, 0.18f), new Vector3(0f, 0f, -12f * sign));
                Part(PrimitiveType.Capsule, "Upper", arm, coat, new Vector3(0f, -0.14f, 0f), new Vector3(0.12f, 0.15f, 0.12f));
                Transform elbow = Joint("Elbow" + side, arm, new Vector3(0f, -0.29f, 0f));
                Part(PrimitiveType.Capsule, "Fore", elbow, coat, new Vector3(0f, -0.13f, 0f), new Vector3(0.11f, 0.14f, 0.11f));
                Part(PrimitiveType.Cylinder, "Cuff", elbow, leather, new Vector3(0f, -0.21f, 0f), new Vector3(0.125f, 0.03f, 0.125f));
                Part(PrimitiveType.Sphere, "Hand", elbow, skin, new Vector3(0f, -0.3f, 0f), new Vector3(0.1f, 0.11f, 0.1f));
                return new[] { arm, elbow };
            }
            Transform[] armL = Arm("L", -0.26f);
            Transform[] armR = Arm("R", 0.26f);

            if (look.melee)
            {
                // Melee: a long blade in the right hand and heavy pauldrons, readable from far away.
                Material steel = LitMat("CharSteel", lit, 0xC9CED6, 0.7f);
                EditorUtility.SetDirty(steel);
                Transform grip = Joint("Grip", armR[1], new Vector3(0f, -0.32f, 0.02f));
                grip.localRotation = Quaternion.Euler(70f, 0f, 0f);
                Part(PrimitiveType.Cylinder, "Hilt", grip, leather, new Vector3(0f, 0f, 0f), new Vector3(0.045f, 0.07f, 0.045f));
                Part(PrimitiveType.Cube, "Guard", grip, steel, new Vector3(0f, 0.08f, 0f), new Vector3(0.2f, 0.035f, 0.05f));
                Part(PrimitiveType.Cube, "Blade", grip, steel, new Vector3(0f, 0.48f, 0f), new Vector3(0.07f, 0.75f, 0.02f));
                Part(PrimitiveType.Sphere, "PauldronL", armL[0], coatDark, new Vector3(-0.02f, 0.06f, 0f), new Vector3(0.24f, 0.16f, 0.24f));
                Part(PrimitiveType.Sphere, "PauldronR", armR[0], coatDark, new Vector3(0.02f, 0.06f, 0f), new Vector3(0.24f, 0.16f, 0.24f));
            }
            else if (look.fire)
            {
                // Fire mage: a crown of flame on the head and a fireball in each hand (not flashed: they glow).
                Vector3[] tips = { new Vector3(0f, 0.38f, -0.02f), new Vector3(-0.08f, 0.34f, 0.02f), new Vector3(0.08f, 0.35f, -0.04f), new Vector3(0f, 0.33f, -0.11f) };
                Vector3[] tilt = { new Vector3(-8f, 0f, 0f), new Vector3(0f, 0f, 22f), new Vector3(0f, 0f, -22f), new Vector3(-30f, 0f, 0f) };
                for (int i = 0; i < tips.Length; i++)
                {
                    GameObject f = Part(PrimitiveType.Sphere, "Flame" + i, neck, i == 0 ? focus : hair, tips[i], new Vector3(0.09f, 0.2f - i * 0.02f, 0.09f), tilt[i], false);
                    f.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
                foreach (Transform elbow in new[] { armL[1], armR[1] })
                {
                    GameObject ball = Part(PrimitiveType.Sphere, "Fireball", elbow, focus, new Vector3(0f, -0.36f, 0.04f), Vector3.one * 0.14f, default, false);
                    ball.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            else
            {
                // Focus stone in the right hand (not flashed: it already glows).
                GameObject stone = Part(PrimitiveType.Sphere, "Focus", armR[1], focus, new Vector3(0f, -0.36f, 0.04f), Vector3.one * 0.11f, default, false);
                stone.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

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
