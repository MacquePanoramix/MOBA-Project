using Moba.Duel;
using UnityEngine;

namespace Moba.LaneSlice
{
    public sealed partial class LaneWorld
    {
        private void BuildEnvironment(LaneMap map)
        {
            var geo = new DuelGeometry();

            float hl = map.HalfLength;
            float hw = map.HalfWidth;
            Color darkFloor = new Color(0.16f, 0.22f, 0.15f);
            Color laneStone = new Color(0.30f, 0.32f, 0.27f);
            Color grassEdge = new Color(0.20f, 0.28f, 0.17f);

            // Base terrain quad
            geo.Quad(
                new Vector3(-hl - 3f, 0f, -hw - 3f),
                new Vector3(-hl - 3f, 0f, hw + 3f),
                new Vector3(hl + 3f, 0f, hw + 3f),
                new Vector3(hl + 3f, 0f, -hw - 3f),
                Vector3.up, darkFloor);

            // Central lane path
            geo.Quad(
                new Vector3(-hl, 0.01f, -3.5f),
                new Vector3(-hl, 0.01f, 3.5f),
                new Vector3(hl, 0.01f, 3.5f),
                new Vector3(hl, 0.01f, -3.5f),
                Vector3.up, laneStone);

            // Flank woodland turf strips
            geo.Quad(
                new Vector3(-hl, 0.005f, 3.5f),
                new Vector3(-hl, 0.005f, hw),
                new Vector3(hl, 0.005f, hw),
                new Vector3(hl, 0.005f, 3.5f),
                Vector3.up, grassEdge);
            geo.Quad(
                new Vector3(-hl, 0.005f, -hw),
                new Vector3(-hl, 0.005f, -3.5f),
                new Vector3(hl, 0.005f, -3.5f),
                new Vector3(hl, 0.005f, -hw),
                Vector3.up, grassEdge);

            // Midline glyph & river crossing marker
            geo.Oval(new Vector3(0f, 0.02f, 0f), new Vector3(1.65f, 0f, 1.65f), 20, new Color(0.42f, 0.45f, 0.36f));

            // Fountains (Blue left, Red right)
            AddFountain(geo, new Vector3(-21.2f, 0.03f, 0f), new Color(0.22f, 0.62f, 0.96f), new Color(0.45f, 0.85f, 1f));
            AddFountain(geo, new Vector3(21.2f, 0.03f, 0f), new Color(0.88f, 0.28f, 0.24f), new Color(1f, 0.56f, 0.48f));

            // Boundary low timber borders
            Color borderColor = new Color(0.24f, 0.19f, 0.14f);
            AddWallBar(geo, new Vector3(0f, 0f, hw + 0.35f), new Vector2(hl * 2f + 1.5f, 0.5f), 0.7f, borderColor);
            AddWallBar(geo, new Vector3(0f, 0f, -hw - 0.35f), new Vector2(hl * 2f + 1.5f, 0.5f), 0.7f, borderColor);
            AddWallBar(geo, new Vector3(-hl - 0.35f, 0f, 0f), new Vector2(0.5f, hw * 2f + 1.5f), 0.7f, borderColor);
            AddWallBar(geo, new Vector3(hl + 0.35f, 0f, 0f), new Vector2(0.5f, hw * 2f + 1.5f), 0.7f, borderColor);

            // Side grove pine trees matching LaneMap blockers
            for (int i = 0; i < map.Trees.Count; i++)
            {
                Vector2 t = map.Trees[i];
                AddPineTree(geo, new Vector3(t.x, 0f, t.y), (i % 3) * 0.15f);
            }

            Mesh envMesh = geo.Bake("LaneEnvMesh");
            ownedMeshes.Add(envMesh);
            var envObj = new GameObject("LaneEnvironment");
            envObj.transform.SetParent(root, false);
            envObj.transform.localPosition = worldOffset;
            envObj.AddComponent<MeshFilter>().sharedMesh = envMesh;
            envObj.AddComponent<MeshRenderer>().sharedMaterial = vertexMat;
        }

        private void BuildPreviewFloor()
        {
            var geo = new DuelGeometry();
            Color baseFloor = new Color(0.11f, 0.14f, 0.16f);
            Color centerPad = new Color(0.17f, 0.21f, 0.23f);
            geo.Quad(
                new Vector3(-11f, 0f, -7f),
                new Vector3(-11f, 0f, 7f),
                new Vector3(11f, 0f, 7f),
                new Vector3(11f, 0f, -7f),
                Vector3.up, baseFloor);
            geo.Quad(
                new Vector3(-9f, 0.01f, -5.2f),
                new Vector3(-9f, 0.01f, 5.2f),
                new Vector3(9f, 0.01f, 5.2f),
                new Vector3(9f, 0.01f, -5.2f),
                Vector3.up, centerPad);

            Mesh floorMesh = geo.Bake("LanePreviewFloorMesh");
            ownedMeshes.Add(floorMesh);
            var floorObj = new GameObject("PreviewFloor");
            floorObj.transform.SetParent(root, false);
            floorObj.transform.localPosition = worldOffset;
            floorObj.AddComponent<MeshFilter>().sharedMesh = floorMesh;
            floorObj.AddComponent<MeshRenderer>().sharedMaterial = vertexMat;

            foreach (float rad in new[] { 2f, 4f, 6f })
            {
                GameObject r = CreateUnlitMesh($"PreviewRange_{rad}", ringMesh, new Color(0.32f, 0.44f, 0.54f, 0.28f));
                r.transform.position = worldOffset + new Vector3(0f, 0.02f, 0f);
                r.transform.localScale = new Vector3(rad, 1f, rad);
            }
        }

        private void BuildTowers(LaneMap map)
        {
            BuildSingleTower(10, LaneTeam.Blue, map.TowerPos(LaneTeam.Blue), new Color(0.28f, 0.65f, 0.98f));
            BuildSingleTower(11, LaneTeam.Red, map.TowerPos(LaneTeam.Red), new Color(0.95f, 0.34f, 0.28f));
        }

        private void BuildSingleTower(int id, LaneTeam team, Vector2 pos, Color teamColor)
        {
            var aliveGeo = new DuelGeometry();
            Color stone = new Color(0.45f, 0.46f, 0.48f);
            aliveGeo.Column(Vector3.zero, 0.55f, new Vector2(1.05f, 1.05f), new Vector2(0.85f, 0.85f), 8, stone);
            aliveGeo.Column(new Vector3(0f, 0.55f, 0f), 2.15f, new Vector2(0.72f, 0.72f), new Vector2(0.56f, 0.56f), 8, stone);
            aliveGeo.Column(new Vector3(0f, 2.70f, 0f), 0.35f, new Vector2(0.82f, 0.82f), new Vector2(0.65f, 0.65f), 8, teamColor);

            Mesh aliveMesh = aliveGeo.Bake($"TowerAlive_{team}");
            ownedMeshes.Add(aliveMesh);
            var aliveObj = new GameObject($"Tower_{team}");
            aliveObj.transform.SetParent(root, false);
            aliveObj.transform.position = ToWorld(pos.x, pos.y);
            aliveObj.AddComponent<MeshFilter>().sharedMesh = aliveMesh;
            aliveObj.AddComponent<MeshRenderer>().sharedMaterial = vertexMat;

            var cryGeo = new DuelGeometry();
            cryGeo.Column(new Vector3(0f, -0.35f, 0f), 0.70f, new Vector2(0.32f, 0.32f), new Vector2(0.05f, 0.05f), 6, teamColor * 1.15f);
            Mesh cryMesh = cryGeo.Bake($"TowerCrystal_{team}");
            ownedMeshes.Add(cryMesh);
            var crystalObj = new GameObject("Crystal");
            crystalObj.transform.SetParent(aliveObj.transform, false);
            crystalObj.transform.localPosition = new Vector3(0f, 3.45f, 0f);
            crystalObj.AddComponent<MeshFilter>().sharedMesh = cryMesh;
            crystalObj.AddComponent<MeshRenderer>().sharedMaterial = vertexMat;

            var ruinGeo = new DuelGeometry();
            ruinGeo.Column(Vector3.zero, 0.32f, new Vector2(1.05f, 1.05f), new Vector2(0.75f, 0.75f), 8, new Color(0.28f, 0.27f, 0.27f));
            Mesh ruinMesh = ruinGeo.Bake($"TowerRuin_{team}");
            ownedMeshes.Add(ruinMesh);
            var ruinObj = new GameObject($"TowerRuin_{team}");
            ruinObj.transform.SetParent(root, false);
            ruinObj.transform.position = ToWorld(pos.x, pos.y);
            ruinObj.AddComponent<MeshFilter>().sharedMesh = ruinMesh;
            ruinObj.AddComponent<MeshRenderer>().sharedMaterial = vertexMat;
            ruinObj.SetActive(false);

            var lr = aliveObj.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.startWidth = 0.09f;
            lr.endWidth = 0.04f;
            var mat = new Material(Shader.Find("Sprites/Default"));
            ownedMaterials.Add(mat);
            mat.color = teamColor;
            lr.sharedMaterial = mat;
            lr.enabled = false;

            towerVisuals[id] = new TowerVisual
            {
                aliveObj = aliveObj,
                ruinObj = ruinObj,
                crystal = crystalObj.transform,
                aggroBeam = lr,
                aggroBeamMat = mat,
                normalBeamColor = teamColor,
            };
        }

        private static void AddFountain(DuelGeometry geo, Vector3 center, Color baseCol, Color glowCol)
        {
            geo.Column(center, 0.14f, new Vector2(3.15f, 3.15f), new Vector2(2.95f, 2.95f), 20, baseCol * 0.45f);
            geo.Oval(center + Vector3.up * 0.15f, new Vector3(2.4f, 0f, 2.4f), 18, glowCol);
            geo.Column(center + new Vector3(baseCol.b > baseCol.r ? -1.8f : 1.8f, 0.14f, 0f), 1.9f, new Vector2(0.45f, 0.45f), new Vector2(0.25f, 0.25f), 8, glowCol);
        }

        private static void AddWallBar(DuelGeometry geo, Vector3 center, Vector2 size, float height, Color col)
        {
            float hx = size.x * 0.5f;
            float hz = size.y * 0.5f;
            Vector3 b0 = center + new Vector3(-hx, 0f, -hz);
            Vector3 b1 = center + new Vector3(-hx, 0f, hz);
            Vector3 b2 = center + new Vector3(hx, 0f, hz);
            Vector3 b3 = center + new Vector3(hx, 0f, -hz);
            Vector3 t0 = b0 + Vector3.up * height;
            Vector3 t1 = b1 + Vector3.up * height;
            Vector3 t2 = b2 + Vector3.up * height;
            Vector3 t3 = b3 + Vector3.up * height;

            geo.Quad(t0, t1, t2, t3, Vector3.up, col * 1.1f);
            geo.Quad(b0, b1, t1, t0, Vector3.left, col);
            geo.Quad(b1, b2, t2, t1, Vector3.forward, col);
            geo.Quad(b2, b3, t3, t2, Vector3.right, col);
            geo.Quad(b3, b0, t0, t3, Vector3.back, col);
        }

        private static void AddPineTree(DuelGeometry geo, Vector3 center, float scaleOffset)
        {
            Color trunk = new Color(0.32f, 0.22f, 0.14f);
            Color needle1 = new Color(0.16f, 0.38f, 0.20f);
            Color needle2 = new Color(0.20f, 0.46f, 0.24f);
            float h = 1f + scaleOffset;
            geo.Column(center, 0.9f * h, new Vector2(0.22f, 0.22f), new Vector2(0.16f, 0.16f), 6, trunk);
            geo.Column(center + Vector3.up * (0.85f * h), 1.1f * h, new Vector2(0.86f, 0.86f), new Vector2(0.25f, 0.25f), 7, needle1);
            geo.Column(center + Vector3.up * (1.75f * h), 0.95f * h, new Vector2(0.62f, 0.62f), new Vector2(0.06f, 0.06f), 7, needle2);
        }
    }
}
