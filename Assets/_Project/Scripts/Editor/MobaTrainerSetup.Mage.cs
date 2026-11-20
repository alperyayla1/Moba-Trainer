using System.Collections.Generic;
using MobaTrainer.Core;
using MobaTrainer.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MobaTrainer.EditorTools
{
    /// <summary>
    /// The Arcanist (second playable hero, artillery mage): long indigo robe with gold trim, white hair and short beard,
    /// staff with a teal crystal in the right hand, two rune stones floating behind the head, a book
    /// at the belt. Same primitive style and joint rig as the Wayfarer, plus a StaffGrip pivot that
    /// CharacterAnimator uses for the staff poses (hold / channel / cast).
    /// Designed in Blender from the Wayfarer's own primitives (spec: mage_spec.json, Codex study).
    /// </summary>
    public static partial class MobaTrainerSetup
    {
        private static CharacterBuild BuildMageCharacter(Transform parent, bool autoFace)
        {
            Shader lit = FindShader("Universal Render Pipeline/Lit");
            Material MageSkin = LitMat("MageSkin", lit, 0xE7B894, 0.25f);
            Material MagePants = LitMat("MagePants", lit, 0x3A3E48, 0.15f);
            Material MageLeather = LitMat("MageLeather", lit, 0x43434A, 0.3f);
            Material MageRobe = LitMat("MageRobe", lit, 0x34478A, 0.2f);
            Material MageMantle = LitMat("MageMantle", lit, 0x202C49, 0.2f);
            Material MageGold = LitMat("MageGold", lit, 0xC9A54E, 0.25f);
            Material MageHair = LitMat("MageHair", lit, 0xEEE9DB, 0.1f);
            Material MageInk = LitMat("MageInk", lit, 0x20232C, 0.1f);
            Material MageCrystal = LitMat("MageCrystal", lit, 0x43DCCF, 0.8f);
            MageCrystal.EnableKeyword("_EMISSION");
            MageCrystal.SetColor("_EmissionColor", Palette.Hex(0x43DCCF) * 2.2f);
            MageCrystal.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            Material MageRune = LitMat("MageRune", lit, 0x43DCCF, 0.4f);
            MageRune.EnableKeyword("_EMISSION");
            MageRune.SetColor("_EmissionColor", Palette.Hex(0x43DCCF) * 1.4f);
            MageRune.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            foreach (Material m in new[] { MageSkin, MagePants, MageLeather, MageRobe, MageMantle, MageGold, MageHair, MageInk, MageCrystal, MageRune }) EditorUtility.SetDirty(m);

            var renderers = new List<Renderer>();
            GameObject Part(PrimitiveType type, string name, Transform p, Material mat, Vector3 pos, Vector3 scale, Vector3 euler = default, bool flash = true, bool castShadow = true)
            {
                GameObject g = NewPrimitive(type, name, p, mat);
                g.transform.localPosition = pos;
                g.transform.localScale = scale;
                g.transform.localRotation = Quaternion.Euler(euler);
                var r = g.GetComponent<MeshRenderer>();
                r.shadowCastingMode = castShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
                if (flash) renderers.Add(r);
                return g;
            }
            Transform Joint(string name, Transform p, Vector3 pos)
            {
                var j = new GameObject(name).transform;
                j.SetParent(p, false);
                j.localPosition = pos;
                return j;
            }

            Transform Model = Joint("MageModel", parent, new Vector3(0f, 0f, 0f));
            Model.localScale = Vector3.one * CharacterScale;
            Transform Rig = Joint("Rig", Model, new Vector3(0f, 0f, 0f));
            Transform Hips = Joint("Hips", Rig, new Vector3(0f, 0.92f, 0f));
            Transform LegL = Joint("LegL", Hips, new Vector3(-0.1f, -0.03f, 0f));
            Transform KneeL = Joint("KneeL", LegL, new Vector3(0f, -0.42f, 0f));
            Transform LegR = Joint("LegR", Hips, new Vector3(0.1f, -0.03f, 0f));
            Transform KneeR = Joint("KneeR", LegR, new Vector3(0f, -0.42f, 0f));
            Transform Spine = Joint("Spine", Hips, new Vector3(0f, 0.06f, 0f));
            Transform Neck = Joint("Neck", Spine, new Vector3(0f, 0.54f, 0f));
            Transform ArmL = Joint("ArmL", Spine, new Vector3(-0.26f, 0.47f, 0f));
            Transform ElbowL = Joint("ElbowL", ArmL, new Vector3(0f, -0.29f, 0f));
            Transform ArmR = Joint("ArmR", Spine, new Vector3(0.26f, 0.47f, 0f));
            Transform ElbowR = Joint("ElbowR", ArmR, new Vector3(0f, -0.29f, 0f));
            Transform StaffGrip = Joint("StaffGrip", ElbowR, new Vector3(0.025f, -0.3f, 0.015f));
            Transform CrystalShape = Joint("CrystalShape", StaffGrip, new Vector3(0f, 1.015f, 0f));
            CrystalShape.localScale = new Vector3(0.6f, 1.5f, 0.6f);

            Part(PrimitiveType.Sphere, "Pelvis", Hips, MagePants, new Vector3(0f, 0f, 0f), new Vector3(0.34f, 0.2f, 0.24f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Capsule, "ThighL", LegL, MagePants, new Vector3(0f, -0.21f, 0f), new Vector3(0.15f, 0.22f, 0.15f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Capsule, "ShinL", KneeL, MagePants, new Vector3(0f, -0.2f, 0f), new Vector3(0.13f, 0.2f, 0.13f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Capsule, "BootL", KneeL, MageLeather, new Vector3(0f, -0.43f, 0.04f), new Vector3(0.15f, 0.13f, 0.13f), new Vector3(90f, 0f, 0f), true, true);
            Part(PrimitiveType.Capsule, "ThighR", LegR, MagePants, new Vector3(0f, -0.21f, 0f), new Vector3(0.15f, 0.22f, 0.15f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Capsule, "ShinR", KneeR, MagePants, new Vector3(0f, -0.2f, 0f), new Vector3(0.13f, 0.2f, 0.13f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Capsule, "BootR", KneeR, MageLeather, new Vector3(0f, -0.43f, 0.04f), new Vector3(0.15f, 0.13f, 0.13f), new Vector3(90f, 0f, 0f), true, true);
            Part(PrimitiveType.Capsule, "Torso", Spine, MageRobe, new Vector3(0f, 0.27f, 0f), new Vector3(0.4f, 0.27f, 0.27f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Cylinder, "Belt", Spine, MageLeather, new Vector3(0f, 0.04f, 0f), new Vector3(0.43f, 0.03f, 0.3f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Cube, "Buckle", Spine, MageGold, new Vector3(0.025f, 0.04f, 0.158f), new Vector3(0.075f, 0.075f, 0.028f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Cube, "Book", Spine, MageMantle, new Vector3(-0.247f, -0.025f, -0.025f), new Vector3(0.09f, 0.2f, 0.16f), new Vector3(0f, 0f, 8f), true, true);
            Part(PrimitiveType.Cube, "BookPages", Spine, MageGold, new Vector3(-0.254f, -0.025f, 0.057f), new Vector3(0.07f, 0.165f, 0.012f), new Vector3(0f, 0f, 8f), true, true);
            Part(PrimitiveType.Sphere, "Head", Neck, MageSkin, new Vector3(0f, 0.15f, 0f), new Vector3(0.25f, 0.27f, 0.25f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Sphere, "EyeL", Neck, MageInk, new Vector3(-0.05f, 0.16f, 0.115f), new Vector3(0.035f, 0.035f, 0.035f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Sphere, "EyeR", Neck, MageInk, new Vector3(0.05f, 0.16f, 0.115f), new Vector3(0.035f, 0.035f, 0.035f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Sphere, "ShoulderL", ArmL, MageMantle, new Vector3(0f, 0.025f, 0f), new Vector3(0.22f, 0.14f, 0.23f), new Vector3(0f, 0f, 12f), true, true);
            Part(PrimitiveType.Capsule, "UpperL", ArmL, MageRobe, new Vector3(0f, -0.14f, 0f), new Vector3(0.12f, 0.15f, 0.12f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Capsule, "ForeL", ElbowL, MageRobe, new Vector3(0f, -0.13f, 0f), new Vector3(0.11f, 0.14f, 0.11f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Cylinder, "CuffL", ElbowL, MageGold, new Vector3(0f, -0.21f, 0f), new Vector3(0.13f, 0.035f, 0.13f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Sphere, "HandL", ElbowL, MageSkin, new Vector3(0f, -0.3f, 0f), new Vector3(0.1f, 0.11f, 0.1f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Sphere, "ShoulderR", ArmR, MageMantle, new Vector3(0f, 0.025f, 0f), new Vector3(0.22f, 0.14f, 0.23f), new Vector3(0f, 0f, -12f), true, true);
            Part(PrimitiveType.Capsule, "UpperR", ArmR, MageRobe, new Vector3(0f, -0.14f, 0f), new Vector3(0.12f, 0.15f, 0.12f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Capsule, "ForeR", ElbowR, MageRobe, new Vector3(0f, -0.13f, 0f), new Vector3(0.11f, 0.14f, 0.11f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Cylinder, "CuffR", ElbowR, MageGold, new Vector3(0f, -0.21f, 0f), new Vector3(0.13f, 0.035f, 0.13f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Sphere, "HandR", ElbowR, MageSkin, new Vector3(0f, -0.3f, 0f), new Vector3(0.1f, 0.11f, 0.1f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Cube, "RuneStone0", Neck, MageMantle, new Vector3(-0.276f, 0.34f, -0.12f), new Vector3(0.075f, 0.11f, 0.055f), new Vector3(0f, 0f, -20f), true, true);
            Part(PrimitiveType.Cube, "RuneGlow0", Neck, MageRune, new Vector3(-0.276f, 0.34f, -0.091f), new Vector3(0.037f, 0.071f, 0.012f), new Vector3(0f, 0f, -20f), false, false);
            Part(PrimitiveType.Cube, "RuneStone1", Neck, MageMantle, new Vector3(0.276f, 0.37f, -0.12f), new Vector3(0.075f, 0.11f, 0.055f), new Vector3(0f, 0f, 20f), true, true);
            Part(PrimitiveType.Cube, "RuneGlow1", Neck, MageRune, new Vector3(0.276f, 0.37f, -0.091f), new Vector3(0.037f, 0.071f, 0.012f), new Vector3(0f, 0f, 20f), false, false);
            Part(PrimitiveType.Cylinder, "StaffShaft", StaffGrip, MageLeather, new Vector3(0f, 0.04f, 0f), new Vector3(0.045f, 0.85f, 0.045f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Sphere, "StaffFoot", StaffGrip, MageGold, new Vector3(0f, -0.81f, 0f), new Vector3(0.065f, 0.075f, 0.065f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Cylinder, "StaffFerrule", StaffGrip, MageGold, new Vector3(0f, 0.79f, 0f), new Vector3(0.063f, 0.051f, 0.063f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Cube, "RuneBack0", Neck, MageRune, new Vector3(-0.276f, 0.34f, -0.151f), new Vector3(0.037f, 0.071f, 0.012f), new Vector3(0f, 0f, -20f), false, false);
            Part(PrimitiveType.Cube, "RuneTop0", Neck, MageRune, new Vector3(-0.276f, 0.394f, -0.12f), new Vector3(0.05f, 0.012f, 0.04f), new Vector3(0f, 0f, -20f), false, false);
            Part(PrimitiveType.Cube, "RuneBack1", Neck, MageRune, new Vector3(0.276f, 0.37f, -0.151f), new Vector3(0.037f, 0.071f, 0.012f), new Vector3(0f, 0f, 20f), false, false);
            Part(PrimitiveType.Cube, "RuneTop1", Neck, MageRune, new Vector3(0.276f, 0.424f, -0.12f), new Vector3(0.05f, 0.012f, 0.04f), new Vector3(0f, 0f, 20f), false, false);
            Part(PrimitiveType.Cylinder, "RobeYoke", Spine, MageRobe, new Vector3(0f, -0.035f, -0.015f), new Vector3(0.47f, 0.095f, 0.33f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Cylinder, "RobeSkirt", Spine, MageRobe, new Vector3(0f, -0.205f, -0.015f), new Vector3(0.515f, 0.125f, 0.38f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Cylinder, "RobeSkirtFlare", Spine, MageRobe, new Vector3(0f, -0.365f, -0.015f), new Vector3(0.55f, 0.105f, 0.4f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Capsule, "RobeFoldL", LegL, MageRobe, new Vector3(-0.015f, -0.21f, -0.025f), new Vector3(0.27f, 0.24f, 0.32f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Cylinder, "RobeLowerL", KneeL, MageRobe, new Vector3(-0.015f, -0.16f, -0.025f), new Vector3(0.345f, 0.225f, 0.405f), new Vector3(0f, 0f, -8f), true, true);
            Part(PrimitiveType.Cylinder, "HemL", KneeL, MageGold, new Vector3(-0.04353f, -0.363005f, -0.025f), new Vector3(0.351f, 0.024f, 0.411f), new Vector3(0f, 0f, -8f), true, true);
            Part(PrimitiveType.Capsule, "RobeTailL", KneeL, MageMantle, new Vector3(-0.02f, -0.235f, -0.16f), new Vector3(0.29f, 0.207f, 0.16f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Capsule, "TailHemL", KneeL, MageGold, new Vector3(-0.02f, -0.422f, -0.16f), new Vector3(0.292f, 0.022f, 0.164f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Cube, "LowerFrontBraidL", KneeL, MageGold, new Vector3(0.075f, -0.165f, 0.186f), new Vector3(0.027f, 0.405f, 0.026f), new Vector3(0f, 0f, -8f), true, true);
            Part(PrimitiveType.Capsule, "RobeFoldR", LegR, MageRobe, new Vector3(0.015f, -0.21f, -0.025f), new Vector3(0.27f, 0.24f, 0.32f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Cylinder, "RobeLowerR", KneeR, MageRobe, new Vector3(0.015f, -0.16f, -0.025f), new Vector3(0.345f, 0.225f, 0.405f), new Vector3(0f, 0f, 8f), true, true);
            Part(PrimitiveType.Cylinder, "HemR", KneeR, MageGold, new Vector3(0.04353f, -0.363005f, -0.025f), new Vector3(0.351f, 0.024f, 0.411f), new Vector3(0f, 0f, 8f), true, true);
            Part(PrimitiveType.Capsule, "RobeTailR", KneeR, MageMantle, new Vector3(0.02f, -0.235f, -0.16f), new Vector3(0.29f, 0.207f, 0.16f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Capsule, "TailHemR", KneeR, MageGold, new Vector3(0.02f, -0.422f, -0.16f), new Vector3(0.292f, 0.022f, 0.164f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Cube, "LowerFrontBraidR", KneeR, MageGold, new Vector3(-0.075f, -0.165f, 0.186f), new Vector3(0.027f, 0.405f, 0.026f), new Vector3(0f, 0f, 8f), true, true);
            Part(PrimitiveType.Cube, "FrontBraidChest", Spine, MageGold, new Vector3(0f, 0.287f, 0.14f), new Vector3(0.035f, 0.49f, 0.024f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Cube, "FrontBraidWaist", Spine, MageGold, new Vector3(0f, -0.035f, 0.154f), new Vector3(0.035f, 0.11f, 0.025f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Cube, "FrontBraidSkirt", Spine, MageGold, new Vector3(0f, -0.205f, 0.183f), new Vector3(0.035f, 0.25f, 0.025f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Cube, "FrontBraidFlare", Spine, MageGold, new Vector3(0f, -0.365f, 0.199f), new Vector3(0.035f, 0.21f, 0.025f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Capsule, "BackPanel", Spine, MageMantle, new Vector3(0f, -0.2f, -0.18f), new Vector3(0.18f, 0.23f, 0.055f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Cube, "CollarBack", Spine, MageMantle, new Vector3(0f, 0.5f, -0.13f), new Vector3(0.33f, 0.13f, 0.065f), new Vector3(-8f, 0f, 0f), true, true);
            Part(PrimitiveType.Cube, "CollarBackEdge", Spine, MageGold, new Vector3(0f, 0.565f, -0.13f), new Vector3(0.337f, 0.024f, 0.07f), new Vector3(-8f, 0f, 0f), true, true);
            Part(PrimitiveType.Cube, "CollarL", Spine, MageMantle, new Vector3(-0.145f, 0.49f, -0.015f), new Vector3(0.06f, 0.13f, 0.23f), new Vector3(0f, 0f, 8f), true, true);
            Part(PrimitiveType.Cube, "CollarEdgeL", Spine, MageGold, new Vector3(-0.145f, 0.555f, -0.015f), new Vector3(0.064f, 0.025f, 0.235f), new Vector3(0f, 0f, 8f), true, true);
            Part(PrimitiveType.Cube, "CollarR", Spine, MageMantle, new Vector3(0.145f, 0.49f, -0.015f), new Vector3(0.06f, 0.13f, 0.23f), new Vector3(0f, 0f, -8f), true, true);
            Part(PrimitiveType.Cube, "CollarEdgeR", Spine, MageGold, new Vector3(0.145f, 0.555f, -0.015f), new Vector3(0.064f, 0.025f, 0.235f), new Vector3(0f, 0f, -8f), true, true);
            Part(PrimitiveType.Sphere, "HairBack", Neck, MageHair, new Vector3(0f, 0.095f, -0.09f), new Vector3(0.28f, 0.4f, 0.15f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Sphere, "HairPartL", Neck, MageHair, new Vector3(-0.052f, 0.257f, -0.025f), new Vector3(0.155f, 0.108f, 0.22f), new Vector3(0f, 0f, 15f), true, true);
            Part(PrimitiveType.Capsule, "HairLockL", Neck, MageHair, new Vector3(-0.119f, 0.055f, -0.03f), new Vector3(0.075f, 0.17f, 0.125f), new Vector3(0f, 0f, -5f), true, true);
            Part(PrimitiveType.Capsule, "BrowL", Neck, MageHair, new Vector3(-0.05f, 0.204f, 0.113f), new Vector3(0.019f, 0.027f, 0.014f), new Vector3(0f, 0f, -83f), true, true);
            Part(PrimitiveType.Sphere, "HairPartR", Neck, MageHair, new Vector3(0.052f, 0.257f, -0.025f), new Vector3(0.155f, 0.108f, 0.22f), new Vector3(0f, 0f, -15f), true, true);
            Part(PrimitiveType.Capsule, "HairLockR", Neck, MageHair, new Vector3(0.119f, 0.055f, -0.03f), new Vector3(0.075f, 0.17f, 0.125f), new Vector3(0f, 0f, 5f), true, true);
            Part(PrimitiveType.Capsule, "BrowR", Neck, MageHair, new Vector3(0.05f, 0.204f, 0.113f), new Vector3(0.019f, 0.027f, 0.014f), new Vector3(0f, 0f, 83f), true, true);
            Part(PrimitiveType.Sphere, "Beard", Neck, MageHair, new Vector3(0f, 0.034f, 0.086f), new Vector3(0.128f, 0.12f, 0.095f), new Vector3(0f, 0f, 0f), true, true);
            Part(PrimitiveType.Capsule, "BeardTip", Neck, MageHair, new Vector3(0f, -0.004f, 0.077f), new Vector3(0.06f, 0.035f, 0.055f), new Vector3(-12f, 0f, 0f), true, true);
            Part(PrimitiveType.Cube, "StaffCrystal", CrystalShape, MageCrystal, new Vector3(0f, 0f, 0f), new Vector3(0.11f, 0.11f, 0.11f), new Vector3(-35.26439f, 0f, 45f), false, false);
            Part(PrimitiveType.Cube, "CrystalFacet", CrystalShape, MageCrystal, new Vector3(0f, 0f, 0f), new Vector3(0.098f, 0.098f, 0.098f), new Vector3(-35.26439f, 35f, 45f), false, false);
            Part(PrimitiveType.Cylinder, "CrystalRing00", StaffGrip, MageGold, new Vector3(0.081406f, 0.99f, 0.02645f), new Vector3(0.011f, 0.028646f, 0.011f), new Vector3(90f, -18f, 0f), true, true);
            Part(PrimitiveType.Cylinder, "CrystalRing01", StaffGrip, MageGold, new Vector3(0.050312f, 0.99f, 0.069248f), new Vector3(0.011f, 0.028646f, 0.011f), new Vector3(90f, -54f, 0f), true, true);
            Part(PrimitiveType.Cylinder, "CrystalRing02", StaffGrip, MageGold, new Vector3(0f, 0.99f, 0.085595f), new Vector3(0.011f, 0.028646f, 0.011f), new Vector3(90f, -90f, 0f), true, true);
            Part(PrimitiveType.Cylinder, "CrystalRing03", StaffGrip, MageGold, new Vector3(-0.050312f, 0.99f, 0.069248f), new Vector3(0.011f, 0.028646f, 0.011f), new Vector3(90f, -126f, 0f), true, true);
            Part(PrimitiveType.Cylinder, "CrystalRing04", StaffGrip, MageGold, new Vector3(-0.081406f, 0.99f, 0.02645f), new Vector3(0.011f, 0.028646f, 0.011f), new Vector3(90f, -162f, 0f), true, true);
            Part(PrimitiveType.Cylinder, "CrystalRing05", StaffGrip, MageGold, new Vector3(-0.081406f, 0.99f, -0.02645f), new Vector3(0.011f, 0.028646f, 0.011f), new Vector3(90f, 162f, 0f), true, true);
            Part(PrimitiveType.Cylinder, "CrystalRing06", StaffGrip, MageGold, new Vector3(-0.050312f, 0.99f, -0.069248f), new Vector3(0.011f, 0.028646f, 0.011f), new Vector3(90f, 126f, 0f), true, true);
            Part(PrimitiveType.Cylinder, "CrystalRing07", StaffGrip, MageGold, new Vector3(0f, 0.99f, -0.085595f), new Vector3(0.011f, 0.028646f, 0.011f), new Vector3(90f, 90f, 0f), true, true);
            Part(PrimitiveType.Cylinder, "CrystalRing08", StaffGrip, MageGold, new Vector3(0.050312f, 0.99f, -0.069248f), new Vector3(0.011f, 0.028646f, 0.011f), new Vector3(90f, 54f, 0f), true, true);
            Part(PrimitiveType.Cylinder, "CrystalRing09", StaffGrip, MageGold, new Vector3(0.081406f, 0.99f, -0.02645f), new Vector3(0.011f, 0.028646f, 0.011f), new Vector3(90f, 18f, 0f), true, true);

            // Idle preview. Runtime CharacterAnimator requires the mage pose branch described in the spec.
            ArmL.localRotation = Quaternion.Euler(new Vector3(0f, 0f, -7f));
            ElbowL.localRotation = Quaternion.Euler(new Vector3(-14f, 0f, 0f));
            ArmR.localRotation = Quaternion.Euler(new Vector3(-10f, 0f, 7f));
            ElbowR.localRotation = Quaternion.Euler(new Vector3(-20f, 0f, 0f));
            StaffGrip.localRotation = Quaternion.Euler(new Vector3(30f, 0f, -4f));

            var anim = Model.gameObject.AddComponent<CharacterAnimator>();
            anim.rigRoot = Rig;
            anim.hips = Hips; anim.spine = Spine; anim.neck = Neck;
            anim.legL = LegL; anim.kneeL = KneeL; anim.legR = LegR; anim.kneeR = KneeR;
            anim.armL = ArmL; anim.elbowL = ElbowL; anim.armR = ArmR; anim.elbowR = ElbowR;
            anim.staffGrip = StaffGrip;
            anim.autoFace = autoFace;
            return new CharacterBuild { model = Model, animator = anim, flashRenderers = renderers.ToArray() };
        }
    }
}
