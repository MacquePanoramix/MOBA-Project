using System.Collections.Generic;
using Moba.Duel;
using UnityEngine;

namespace Moba.LaneSlice
{
    public sealed partial class LaneWorld
    {
        private sealed class HeroRig
        {
            public Transform root;
            public Transform bodyPivot;
            public Transform rightArmPivot;
            public Transform leftArmPivot;
            public Transform leftLegPivot;
            public Transform rightLegPivot;
            public MeshRenderer[] renderers;
        }

        private static HeroRig BuildHeroRig(LaneHeroId heroId, string name, Transform parent, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderers = new List<MeshRenderer>();

            var rig = new HeroRig { root = go.transform };

            MeshRenderer AddPart(string partName, Transform partParent, Vector3 localPos, Mesh mesh)
            {
                var p = new GameObject(partName);
                p.transform.SetParent(partParent, false);
                p.transform.localPosition = localPos;
                p.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = p.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                renderers.Add(mr);
                return mr;
            }

            Transform NewPivot(string pivotName, Transform pivotParent, Vector3 localPos)
            {
                var p = new GameObject(pivotName);
                p.transform.SetParent(pivotParent, false);
                p.transform.localPosition = localPos;
                return p.transform;
            }

            if (heroId == LaneHeroId.Ogre)
            {
                Color skin = new Color(0.42f, 0.56f, 0.34f);
                Color leather = new Color(0.36f, 0.24f, 0.16f);
                Color club = new Color(0.48f, 0.31f, 0.18f);

                rig.leftLegPivot = NewPivot("LeftLegPivot", go.transform, new Vector3(-0.24f, 0.72f, 0f));
                rig.rightLegPivot = NewPivot("RightLegPivot", go.transform, new Vector3(0.24f, 0.72f, 0f));
                var legGeo = new DuelGeometry();
                legGeo.Column(new Vector3(0f, -0.72f, 0f), 0.72f, new Vector2(0.22f, 0.22f), new Vector2(0.18f, 0.18f), 7, leather);
                Mesh legMesh = legGeo.Bake("OgreLeg");
                AddPart("LeftLeg", rig.leftLegPivot, Vector3.zero, legMesh);
                AddPart("RightLeg", rig.rightLegPivot, Vector3.zero, legMesh);

                rig.bodyPivot = NewPivot("BodyPivot", go.transform, new Vector3(0f, 0.66f, 0f));
                var bodyGeo = new DuelGeometry();
                bodyGeo.Column(Vector3.zero, 0.92f, new Vector2(0.54f, 0.44f), new Vector2(0.44f, 0.36f), 8, skin);
                bodyGeo.Column(new Vector3(0f, 0.86f, 0.08f), 0.42f, new Vector2(0.26f, 0.24f), new Vector2(0.22f, 0.20f), 8, skin * 1.06f);
                bodyGeo.Column(new Vector3(0f, 0.04f, 0f), 0.16f, new Vector2(0.55f, 0.45f), new Vector2(0.53f, 0.43f), 8, leather);
                AddPart("Torso", rig.bodyPivot, Vector3.zero, bodyGeo.Bake("OgreTorso"));

                rig.leftArmPivot = NewPivot("LeftArmPivot", rig.bodyPivot, new Vector3(-0.48f, 0.72f, 0.04f));
                var lArmGeo = new DuelGeometry();
                lArmGeo.Beam(Vector3.zero, new Vector3(-0.08f, -0.50f, 0.24f), 0.15f, 0.12f, skin, 7);
                AddPart("LeftArm", rig.leftArmPivot, Vector3.zero, lArmGeo.Bake("OgreLeftArm"));

                rig.rightArmPivot = NewPivot("RightArmPivot", rig.bodyPivot, new Vector3(0.48f, 0.72f, 0.04f));
                var rArmGeo = new DuelGeometry();
                rArmGeo.Beam(Vector3.zero, new Vector3(0.04f, -0.34f, 0.22f), 0.15f, 0.12f, skin, 7);
                rArmGeo.Beam(new Vector3(0.04f, -0.34f, 0.22f), new Vector3(0.08f, -0.52f, 1.12f), 0.09f, 0.19f, club, 7);
                AddPart("RightArmClub", rig.rightArmPivot, Vector3.zero, rArmGeo.Bake("OgreClubArm"));
            }
            else if (heroId == LaneHeroId.Miravel)
            {
                Color robe = new Color(0.25f, 0.62f, 0.76f);
                Color shawl = new Color(0.86f, 0.90f, 0.75f);
                Color staff = new Color(0.55f, 0.38f, 0.20f);
                Color gem = new Color(0.38f, 1f, 0.72f);

                rig.leftLegPivot = NewPivot("LeftLegPivot", go.transform, new Vector3(-0.14f, 0.45f, 0f));
                rig.rightLegPivot = NewPivot("RightLegPivot", go.transform, new Vector3(0.14f, 0.45f, 0f));
                rig.bodyPivot = NewPivot("BodyPivot", go.transform, Vector3.zero);

                var bodyGeo = new DuelGeometry();
                bodyGeo.Column(Vector3.zero, 1.25f, new Vector2(0.38f, 0.38f), new Vector2(0.22f, 0.22f), 8, robe);
                bodyGeo.Column(new Vector3(0f, 1.25f, 0.02f), 0.36f, new Vector2(0.20f, 0.20f), new Vector2(0.16f, 0.16f), 8, shawl);
                AddPart("Robe", rig.bodyPivot, Vector3.zero, bodyGeo.Bake("MiravelRobe"));

                rig.leftArmPivot = NewPivot("LeftArmPivot", rig.bodyPivot, new Vector3(-0.30f, 1.02f, 0.06f));
                var lGeo = new DuelGeometry();
                lGeo.Beam(Vector3.zero, new Vector3(-0.06f, -0.30f, 0.26f), 0.07f, 0.055f, robe, 6);
                lGeo.Column(new Vector3(-0.06f, -0.32f, 0.28f), 0.10f, new Vector2(0.06f, 0.06f), new Vector2(0.05f, 0.05f), 6, shawl);
                AddPart("LeftArm", rig.leftArmPivot, Vector3.zero, lGeo.Bake("MiravelLeftArm"));

                rig.rightArmPivot = NewPivot("RightArmPivot", rig.bodyPivot, new Vector3(0.32f, 1.02f, 0.08f));
                var rGeo = new DuelGeometry();
                rGeo.Beam(new Vector3(0f, -0.64f, 0.02f), new Vector3(0.05f, 0.72f, 0.36f), 0.045f, 0.045f, staff, 6);
                rGeo.Column(new Vector3(0.05f, 0.72f, 0.36f), 0.20f, new Vector2(0.12f, 0.12f), new Vector2(0.05f, 0.05f), 6, gem);
                AddPart("StaffArm", rig.rightArmPivot, Vector3.zero, rGeo.Bake("MiravelStaffArm"));
            }
            else if (heroId == LaneHeroId.Swordsman) // Old Man Swordsman
            {
                Color tunic = new Color(0.24f, 0.26f, 0.34f);
                Color sash = new Color(0.82f, 0.66f, 0.28f);
                Color strawHat = new Color(0.76f, 0.64f, 0.40f);
                Color steel = new Color(0.88f, 0.92f, 0.98f);

                rig.leftLegPivot = NewPivot("LeftLegPivot", go.transform, new Vector3(-0.16f, 0.56f, 0f));
                rig.rightLegPivot = NewPivot("RightLegPivot", go.transform, new Vector3(0.16f, 0.56f, 0f));
                var legGeo = new DuelGeometry();
                legGeo.Column(new Vector3(0f, -0.56f, 0f), 0.56f, new Vector2(0.14f, 0.14f), new Vector2(0.12f, 0.12f), 6, tunic * 0.85f);
                Mesh legMesh = legGeo.Bake("SwordLeg");
                AddPart("LeftLeg", rig.leftLegPivot, Vector3.zero, legMesh);
                AddPart("RightLeg", rig.rightLegPivot, Vector3.zero, legMesh);

                rig.bodyPivot = NewPivot("BodyPivot", go.transform, new Vector3(0f, 0.50f, 0f));
                var bodyGeo = new DuelGeometry();
                bodyGeo.Column(Vector3.zero, 0.64f, new Vector2(0.30f, 0.26f), new Vector2(0.25f, 0.22f), 8, tunic);
                bodyGeo.Column(new Vector3(0f, 0.12f, 0f), 0.18f, new Vector2(0.29f, 0.25f), new Vector2(0.29f, 0.25f), 8, sash);
                bodyGeo.Column(new Vector3(0f, 0.64f, 0.04f), 0.28f, new Vector2(0.16f, 0.16f), new Vector2(0.15f, 0.15f), 8, sash * 1.1f);
                bodyGeo.Column(new Vector3(0f, 0.90f, 0.02f), 0.22f, new Vector2(0.46f, 0.46f), new Vector2(0.06f, 0.06f), 10, strawHat);
                AddPart("TorsoHat", rig.bodyPivot, Vector3.zero, bodyGeo.Bake("SwordTorso"));

                rig.leftArmPivot = NewPivot("LeftArmPivot", rig.bodyPivot, new Vector3(-0.32f, 0.42f, 0.06f));
                var lGeo = new DuelGeometry();
                lGeo.Beam(Vector3.zero, new Vector3(-0.08f, -0.02f, 0.94f), 0.048f, 0.022f, steel, 5);
                lGeo.Column(new Vector3(-0.01f, -0.04f, 0.14f), 0.08f, new Vector2(0.07f, 0.07f), new Vector2(0.07f, 0.07f), 6, sash);
                AddPart("LeftBlade", rig.leftArmPivot, Vector3.zero, lGeo.Bake("SwordLeftBlade"));

                rig.rightArmPivot = NewPivot("RightArmPivot", rig.bodyPivot, new Vector3(0.32f, 0.44f, 0.06f));
                var rGeo = new DuelGeometry();
                rGeo.Beam(Vector3.zero, new Vector3(0.08f, -0.02f, 0.96f), 0.048f, 0.022f, steel, 5);
                rGeo.Column(new Vector3(0.01f, -0.04f, 0.14f), 0.08f, new Vector2(0.07f, 0.07f), new Vector2(0.07f, 0.07f), 6, sash);
                AddPart("RightBlade", rig.rightArmPivot, Vector3.zero, rGeo.Bake("SwordRightBlade"));
            }
            else // Chain Laser Shooter
            {
                Color coat = new Color(0.16f, 0.24f, 0.38f);
                Color trim = new Color(0.36f, 0.56f, 0.74f);
                Color prism = new Color(0.26f, 0.95f, 1.0f);
                Color gunmetal = new Color(0.22f, 0.26f, 0.32f);

                rig.leftLegPivot = NewPivot("LeftLegPivot", go.transform, new Vector3(-0.15f, 0.54f, 0f));
                rig.rightLegPivot = NewPivot("RightLegPivot", go.transform, new Vector3(0.15f, 0.54f, 0f));
                var legGeo = new DuelGeometry();
                legGeo.Column(new Vector3(0f, -0.54f, 0f), 0.54f, new Vector2(0.12f, 0.13f), new Vector2(0.10f, 0.10f), 6, gunmetal);
                legGeo.Column(new Vector3(0f, -0.18f, 0.03f), 0.08f, new Vector2(0.11f, 0.11f), new Vector2(0.11f, 0.11f), 6, prism);
                Mesh legMesh = legGeo.Bake("LaserLeg");
                AddPart("LeftLeg", rig.leftLegPivot, Vector3.zero, legMesh);
                AddPart("RightLeg", rig.rightLegPivot, Vector3.zero, legMesh);

                rig.bodyPivot = NewPivot("BodyPivot", go.transform, new Vector3(0f, 0.54f, 0f));
                var bodyGeo = new DuelGeometry();
                bodyGeo.Column(Vector3.zero, 0.66f, new Vector2(0.26f, 0.22f), new Vector2(0.23f, 0.20f), 8, coat);
                bodyGeo.Column(new Vector3(0f, 0.28f, -0.22f), 0.34f, new Vector2(0.16f, 0.10f), new Vector2(0.14f, 0.09f), 6, gunmetal);
                bodyGeo.Column(new Vector3(0f, 0.38f, -0.24f), 0.16f, new Vector2(0.11f, 0.07f), new Vector2(0.09f, 0.06f), 6, prism);
                bodyGeo.Column(new Vector3(0f, 0.66f, 0.02f), 0.28f, new Vector2(0.17f, 0.17f), new Vector2(0.15f, 0.15f), 8, trim);
                bodyGeo.Column(new Vector3(0f, 0.78f, 0.10f), 0.09f, new Vector2(0.16f, 0.12f), new Vector2(0.15f, 0.11f), 6, prism);
                AddPart("TorsoHelm", rig.bodyPivot, Vector3.zero, bodyGeo.Bake("LaserTorso"));

                rig.leftArmPivot = NewPivot("LeftArmPivot", rig.bodyPivot, new Vector3(-0.27f, 0.44f, 0.04f));
                var lGeo = new DuelGeometry();
                lGeo.Beam(Vector3.zero, new Vector3(0.08f, -0.10f, 0.36f), 0.06f, 0.05f, coat, 6);
                lGeo.Column(new Vector3(0.08f, -0.10f, 0.36f), 0.08f, new Vector2(0.06f, 0.06f), new Vector2(0.05f, 0.05f), 6, prism);
                AddPart("LeftArm", rig.leftArmPivot, Vector3.zero, lGeo.Bake("LaserLeftArm"));

                rig.rightArmPivot = NewPivot("RightArmPivot", rig.bodyPivot, new Vector3(0.28f, 0.44f, 0.06f));
                var rGeo = new DuelGeometry();
                rGeo.Beam(Vector3.zero, new Vector3(-0.04f, -0.06f, 0.32f), 0.06f, 0.055f, coat, 6);
                rGeo.Beam(new Vector3(-0.04f, -0.04f, 0.18f), new Vector3(-0.04f, -0.04f, 1.04f), 0.065f, 0.052f, gunmetal, 6);
                rGeo.Beam(new Vector3(-0.04f, 0.02f, 0.56f), new Vector3(-0.04f, 0.02f, 1.12f), 0.036f, 0.026f, prism, 6);
                AddPart("PrismRifle", rig.rightArmPivot, Vector3.zero, rGeo.Bake("LaserPrismRifle"));
            }

            rig.renderers = renderers.ToArray();
            return rig;
        }

        private static Mesh BuildMinionMesh(bool ranged)
        {
            var g = new DuelGeometry();
            Color armor = ranged ? new Color(0.48f, 0.46f, 0.62f) : new Color(0.55f, 0.56f, 0.52f);
            Color crest = new Color(0.88f, 0.86f, 0.80f);
            float h = ranged ? 0.88f : 0.78f;
            float r = ranged ? 0.22f : 0.28f;
            g.Column(Vector3.zero, h, new Vector2(r, r), new Vector2(r * 0.72f, r * 0.72f), 6, armor);
            g.Column(new Vector3(0f, h, 0.02f), 0.24f, new Vector2(0.14f, 0.14f), new Vector2(0.10f, 0.10f), 6, crest);
            if (ranged)
            {
                g.Beam(new Vector3(0.22f, 0.30f, 0.05f), new Vector3(0.24f, 1.15f, 0.28f), 0.03f, 0.03f, crest, 5);
            }
            return g.Bake(ranged ? "RangedMinionMesh" : "MeleeMinionMesh");
        }

        private static Mesh BuildOrbMesh()
        {
            var g = new DuelGeometry();
            Color outer = new Color(0.35f, 1f, 0.68f);
            g.Column(new Vector3(0f, -0.25f, 0f), 0.25f, new Vector2(0.08f, 0.08f), new Vector2(0.32f, 0.32f), 10, outer);
            g.Column(new Vector3(0f, 0f, 0f), 0.25f, new Vector2(0.32f, 0.32f), new Vector2(0.08f, 0.08f), 10, outer * 1.1f);
            return g.Bake("OrbMesh");
        }

        private static Mesh BuildProjectileMesh()
        {
            var g = new DuelGeometry();
            g.Column(new Vector3(0f, -0.14f, 0f), 0.28f, new Vector2(0.15f, 0.15f), new Vector2(0.15f, 0.15f), 8, Color.white);
            return g.Bake("ProjMesh");
        }

        private static Mesh BuildRingMesh(float innerR, float outerR, int sides)
        {
            var g = new DuelGeometry();
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides;
                float a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 i0 = new Vector3(Mathf.Cos(a0) * innerR, 0f, Mathf.Sin(a0) * innerR);
                Vector3 o0 = new Vector3(Mathf.Cos(a0) * outerR, 0f, Mathf.Sin(a0) * outerR);
                Vector3 o1 = new Vector3(Mathf.Cos(a1) * outerR, 0f, Mathf.Sin(a1) * outerR);
                Vector3 i1 = new Vector3(Mathf.Cos(a1) * innerR, 0f, Mathf.Sin(a1) * innerR);
                g.Quad(i0, o0, o1, i1, Vector3.up, Color.white);
            }
            return g.Bake("RingMesh");
        }

        private static Mesh BuildForwardBeamMesh()
        {
            var g = new DuelGeometry();
            g.Quad(
                new Vector3(-0.5f, 0f, 0f),
                new Vector3(-0.5f, 0f, 1f),
                new Vector3(0.5f, 0f, 1f),
                new Vector3(0.5f, 0f, 0f),
                Vector3.up, Color.white);
            return g.Bake("BeamQuadMesh");
        }

        private static Mesh BuildConeMesh(float cosHalfAngle, int segments)
        {
            var g = new DuelGeometry();
            float halfAngle = Mathf.Acos(Mathf.Clamp(cosHalfAngle, -0.95f, 0.98f));
            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.Lerp(-halfAngle, halfAngle, i / (float)segments);
                float a1 = Mathf.Lerp(-halfAngle, halfAngle, (i + 1) / (float)segments);
                Vector3 p0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0));
                Vector3 p1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1));
                g.Face(Vector3.zero, p0, p1, Vector3.up, Color.white);
            }
            return g.Bake("ConeArcMesh");
        }
    }
}
