using System;
using System.Collections.Generic;
using UnityEngine;

namespace Moba.Duel
{
    public sealed partial class DuelWorld : IDisposable
    {
        private static readonly Color SlamFillColor = new Color32(246, 206, 118, 235);
        private static readonly Color MoveRingColor = new Color32(168, 244, 214, 230);
        private static readonly Color PulseGuideColor = new Color32(73, 207, 186, 78);
        private static readonly Color PaperFlash = new Color32(252, 248, 224, 255);

        public Camera Camera { get; private set; }

        private readonly Transform root;
        private readonly DuelTuning tuning;
        private readonly Material fieldMaterial;
        private readonly Material ink;
        private readonly Material lineMaterial;
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<Material> materials = new List<Material>();
        private readonly Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        private readonly Figure ogre;
        private readonly Figure luis;
        private readonly Transform orbRoot;
        private readonly Transform orbCrystal;
        private readonly LineRenderer orbPickupRing;
        private readonly LineRenderer orbPulseGuide;
        private readonly LineRenderer orbBurstRing;
        private readonly LineRenderer slamOutline;
        private readonly LineRenderer slamProgress;
        private readonly LineRenderer sweepOutline;
        private readonly LineRenderer sweepProgress;
        private readonly LineRenderer ogreImmunityRing;
        private readonly LineRenderer luisWindupRing;
        private readonly LineRenderer moveMarker;

        private int viewWidth;
        private int viewHeight;
        private float elapsed;
        private float moveMarkerRemaining;
        private float orbBurstRemaining;
        private float impactFlashRemaining;
        private bool impactWasSlam;
        private Vector2 impactOrigin;
        private Vector2 impactAim;
        private bool ogreWasWindingUp;
        private int seenPickups;
        private int seenDenials;
        private Vector2 lastOrbPosition;
        private bool hadOrb;
        private bool orbBurstWasPickup;

        public DuelWorld(Transform root, Shader surfaceShader, Shader lineShader, DuelTuning tuning)
        {
            this.root = root ? root : throw new ArgumentNullException(nameof(root));
            this.tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

            Shader fieldShader = Resources.Load<Shader>("DuelField")
                ?? Shader.Find("MOBA/Duel field")
                ?? surfaceShader
                ?? Shader.Find("Standard");
            Shader unlitShader = lineShader != null ? lineShader : Shader.Find("Sprites/Default");

            fieldMaterial = TrackMaterial(new Material(fieldShader) { color = Color.white });
            ink = TrackMaterial(new Material(unlitShader) { color = Color.white });
            lineMaterial = TrackMaterial(new Material(unlitShader) { color = Color.white });

            CreateCameraAndLight();
            BuildField();

            ogre = MakeHero(true);
            luis = MakeHero(false);

            orbRoot = Child("Orb", this.root);
            var orbGeo = new DuelGeometry();
            orbGeo.Oval(Vector3.zero, new Vector3(0.24f, 0.34f, 0.24f), 6, MintLight);
            orbGeo.Oval(Vector3.up * 0.04f, new Vector3(0.29f, 0.12f, 0.29f), 6, LuisTint);
            orbCrystal = Model("Orb crystal", orbRoot, orbGeo);
            orbCrystal.localPosition = Vector3.up * 0.62f;

            orbPickupRing = Circle("Orb pickup ring", orbRoot, this.tuning.orbPickupRadius, 0.065f, 0.055f, Gold);
            orbPulseGuide = Circle("Orb pulse guide", orbRoot, this.tuning.orbPulseRadius, 0.052f, 0.040f, PulseGuideColor);
            orbBurstRing = Circle("Orb burst", this.root, 1f, 0.095f, 0.09f, MintLight);

            slamOutline = CreateLine("Slam telegraph", this.root, OgreTint, 0.065f, true);
            slamProgress = CreateLine("Slam progress", this.root, SlamFillColor, 0.095f, false);
            sweepOutline = CreateLine("Sweep telegraph", this.root, OgreTint, 0.065f, true);
            sweepProgress = CreateLine("Sweep progress", this.root, SlamFillColor, 0.095f, false);
            ogreImmunityRing = Circle("Ogre immunity", this.root, 0.88f, 0.088f, 0.068f, Gold);
            luisWindupRing = Circle("Luis wind-up", this.root, 0.78f, 0.088f, 0.065f, MintLight);
            moveMarker = Circle("Move marker", this.root, 0.36f, 0.075f, 0.055f, MoveRingColor);

            HideDynamicElements();
        }

        public bool TryGround(Vector2 screenPoint, out Vector2 groundPoint)
        {
            groundPoint = Vector2.zero;
            if (Camera == null || float.IsNaN(screenPoint.x) || float.IsNaN(screenPoint.y) ||
                !Camera.pixelRect.Contains(screenPoint))
                return false;

            Ray ray = Camera.ScreenPointToRay(screenPoint);
            if (!groundPlane.Raycast(ray, out float distance))
                return false;

            Vector3 hit = ray.GetPoint(distance);
            groundPoint = new Vector2(hit.x, hit.z);
            return true;
        }

        public void SetMovementMarker(Vector2 point)
        {
            if (float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.x) || float.IsInfinity(point.y))
                return;

            moveMarker.transform.localPosition = Ground(point, 0.075f);
            moveMarkerRemaining = 0.38f;
            moveMarker.enabled = true;
        }

        public void Show(DuelFrame frame, DuelHero localHero, float dt)
        {
            ResizeCamera();
            float step = Mathf.Clamp(dt, 0f, 0.25f);
            elapsed += step;

            if (frame == null)
            {
                HideDynamicElements();
                return;
            }

            Pose(ogre, frame.ogre, true, localHero == DuelHero.Ogre, step);
            Pose(luis, frame.luis, false, localHero == DuelHero.Luis, step);

            UpdateOrb(frame, step);
            UpdateAbilityTelegraphs(frame, step);
            UpdateMoveMarker(step);
        }

        private void HideDynamicElements()
        {
            ogre.root.gameObject.SetActive(false);
            luis.root.gameObject.SetActive(false);
            ogre.oldHp = -1f;
            luis.oldHp = -1f;
            ogre.placed = false;
            luis.placed = false;
            ogre.healTime = 0f;
            luis.healTime = 0f;
            ogre.healRing.enabled = false;
            luis.healRing.enabled = false;

            orbRoot.gameObject.SetActive(false);
            orbBurstRing.enabled = false;
            slamOutline.enabled = false;
            slamProgress.enabled = false;
            sweepOutline.enabled = false;
            sweepProgress.enabled = false;
            ogreImmunityRing.enabled = false;
            luisWindupRing.enabled = false;
            moveMarker.enabled = false;

            moveMarkerRemaining = 0f;
            orbBurstRemaining = 0f;
            impactFlashRemaining = 0f;
            ogreWasWindingUp = false;
            seenPickups = 0;
            seenDenials = 0;
            hadOrb = false;
        }

        private void UpdateOrb(DuelFrame frame, float dt)
        {
            if (frame.orbPickups < seenPickups) seenPickups = frame.orbPickups;
            if (frame.orbDenials < seenDenials) seenDenials = frame.orbDenials;

            if (frame.orbPickups > seenPickups)
            {
                seenPickups = frame.orbPickups;
                orbBurstWasPickup = true;
                orbBurstRemaining = 0.36f;
                orbBurstRing.transform.localPosition = Ground(frame.luis.position, 0.095f);
            }
            else if (frame.orbDenials > seenDenials)
            {
                seenDenials = frame.orbDenials;
                orbBurstWasPickup = false;
                orbBurstRemaining = 0.30f;
                Vector2 burstAt = hadOrb ? lastOrbPosition : frame.ogre.position;
                orbBurstRing.transform.localPosition = Ground(burstAt, 0.095f);
            }

            hadOrb = frame.orbActive;
            if (frame.orbActive)
            {
                lastOrbPosition = frame.orb;
                orbRoot.gameObject.SetActive(true);
                orbRoot.localPosition = Ground(frame.orb);
                orbCrystal.localPosition = Vector3.up * (0.62f + Mathf.Sin(elapsed * 3.4f) * 0.085f);
                orbCrystal.localRotation = Quaternion.Euler(14f, elapsed * 70f, 0f);
                orbPickupRing.enabled = true;
                orbPulseGuide.enabled = true;
            }
            else
            {
                orbRoot.gameObject.SetActive(false);
            }

            if (orbBurstRemaining > 0f)
            {
                orbBurstRemaining = Mathf.Max(0f, orbBurstRemaining - dt);
                float duration = orbBurstWasPickup ? 0.36f : 0.30f;
                float t = 1f - orbBurstRemaining / duration;
                float radius = orbBurstWasPickup
                    ? Mathf.Lerp(tuning.orbPickupRadius, tuning.orbPulseRadius, t)
                    : Mathf.Lerp(tuning.orbPickupRadius * 1.15f, 0.12f, t);
                Color c = orbBurstWasPickup ? MintLight : OgreTint;
                c.a = Mathf.Lerp(0.95f, 0f, t);
                orbBurstRing.enabled = true;
                orbBurstRing.transform.localScale = new Vector3(radius, 1f, radius);
                orbBurstRing.startColor = orbBurstRing.endColor = c;
            }
            else
            {
                orbBurstRing.enabled = false;
            }
        }

        private void UpdateAbilityTelegraphs(DuelFrame frame, float dt)
        {
            DuelUnitFrame o = frame.ogre;
            bool oSlam = o.action == (int)DuelOrder.Slam;
            bool oSweep = o.action == (int)DuelOrder.Sweep;
            bool oWinding = (oSlam || oSweep) && !o.recovering;

            if (ogreWasWindingUp && o.recovering && (oSlam || oSweep))
            {
                impactFlashRemaining = 0.18f;
                impactWasSlam = oSlam;
                impactOrigin = o.position;
                impactAim = o.aim.sqrMagnitude > 0.0001f ? o.aim.normalized : Vector2.right;
            }
            ogreWasWindingUp = oWinding;

            if (oWinding)
            {
                Vector2 aim = o.aim.sqrMagnitude > 0.0001f ? o.aim.normalized : Vector2.right;
                float p = Mathf.Clamp01(o.progress);
                if (oSlam)
                {
                    DrawSlamTelegraph(o.position, aim, p, OgreTint, true);
                    sweepOutline.enabled = false;
                    sweepProgress.enabled = false;
                }
                else
                {
                    DrawSweepTelegraph(o.position, aim, p, OgreTint, true);
                    slamOutline.enabled = false;
                    slamProgress.enabled = false;
                }
            }
            else if (impactFlashRemaining > 0f)
            {
                impactFlashRemaining = Mathf.Max(0f, impactFlashRemaining - dt);
                float alpha = impactFlashRemaining / 0.18f;
                Color flash = PaperFlash;
                flash.a = alpha;
                if (impactWasSlam)
                {
                    DrawSlamTelegraph(impactOrigin, impactAim, 1f, flash, false);
                    sweepOutline.enabled = false;
                    sweepProgress.enabled = false;
                }
                else
                {
                    DrawSweepTelegraph(impactOrigin, impactAim, 1f, flash, false);
                    slamOutline.enabled = false;
                    slamProgress.enabled = false;
                }
            }
            else
            {
                slamOutline.enabled = false;
                slamProgress.enabled = false;
                sweepOutline.enabled = false;
                sweepProgress.enabled = false;
            }

            if (o.immune)
            {
                ogreImmunityRing.enabled = true;
                ogreImmunityRing.transform.localPosition = Ground(o.position, 0.088f);
                float pulse = 1f + Mathf.Sin(elapsed * 16f) * 0.05f;
                ogreImmunityRing.transform.localScale = new Vector3(pulse, 1f, pulse);
            }
            else
            {
                ogreImmunityRing.enabled = false;
            }

            DuelUnitFrame l = frame.luis;
            bool lWinding = l.action == (int)DuelOrder.Basic && !l.recovering;
            if (lWinding)
            {
                luisWindupRing.enabled = true;
                luisWindupRing.transform.localPosition = Ground(l.position, 0.088f);
                luisWindupRing.widthMultiplier = Mathf.Lerp(0.045f, 0.135f, Mathf.Clamp01(l.progress));
            }
            else
            {
                luisWindupRing.enabled = false;
            }
        }

        private void DrawSlamTelegraph(Vector2 origin, Vector2 aim, float progress, Color outlineColor, bool showProgress)
        {
            Vector2 side = new Vector2(-aim.y, aim.x) * tuning.slamHalfWidth;
            Vector2 tip = origin + aim * tuning.slamRange;

            slamOutline.enabled = true;
            slamOutline.startColor = slamOutline.endColor = outlineColor;
            slamOutline.positionCount = 4;
            slamOutline.SetPosition(0, Ground(origin + side, 0.082f));
            slamOutline.SetPosition(1, Ground(tip + side, 0.082f));
            slamOutline.SetPosition(2, Ground(tip - side, 0.082f));
            slamOutline.SetPosition(3, Ground(origin - side, 0.082f));

            if (showProgress)
            {
                Vector2 barCenter = origin + aim * (tuning.slamRange * progress);
                slamProgress.enabled = true;
                slamProgress.positionCount = 2;
                slamProgress.SetPosition(0, Ground(barCenter - side, 0.095f));
                slamProgress.SetPosition(1, Ground(barCenter + side, 0.095f));
            }
            else
            {
                slamProgress.enabled = false;
            }
        }

        private void DrawSweepTelegraph(Vector2 origin, Vector2 aim, float progress, Color outlineColor, bool showProgress)
        {
            const int arcSteps = 18;
            float baseAngle = Mathf.Atan2(aim.y, aim.x);
            float halfRad = tuning.sweepHalfAngle * Mathf.Deg2Rad;

            sweepOutline.enabled = true;
            sweepOutline.startColor = sweepOutline.endColor = outlineColor;
            sweepOutline.positionCount = arcSteps + 2;
            sweepOutline.SetPosition(0, Ground(origin, 0.082f));
            for (int i = 0; i <= arcSteps; i++)
            {
                float a = Mathf.Lerp(baseAngle - halfRad, baseAngle + halfRad, i / (float)arcSteps);
                Vector2 p = origin + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * tuning.sweepRange;
                sweepOutline.SetPosition(i + 1, Ground(p, 0.082f));
            }

            if (showProgress)
            {
                float r = Mathf.Max(0.2f, tuning.sweepRange * progress);
                sweepProgress.enabled = true;
                sweepProgress.positionCount = arcSteps + 1;
                for (int i = 0; i <= arcSteps; i++)
                {
                    float a = Mathf.Lerp(baseAngle - halfRad, baseAngle + halfRad, i / (float)arcSteps);
                    Vector2 p = origin + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    sweepProgress.SetPosition(i, Ground(p, 0.095f));
                }
            }
            else
            {
                sweepProgress.enabled = false;
            }
        }

        private void UpdateMoveMarker(float dt)
        {
            if (moveMarkerRemaining <= 0f)
            {
                moveMarker.enabled = false;
                return;
            }

            moveMarkerRemaining = Mathf.Max(0f, moveMarkerRemaining - dt);
            float t = 1f - moveMarkerRemaining / 0.38f;
            float s = Mathf.Lerp(1.1f, 0.45f, t);
            Color c = MoveRingColor;
            c.a = Mathf.Lerp(0.9f, 0f, t);
            moveMarker.enabled = true;
            moveMarker.transform.localScale = new Vector3(s, 1f, s);
            moveMarker.startColor = moveMarker.endColor = c;
        }

        private void CreateCameraAndLight()
        {
            Transform camTransform = Child("Duel camera", root);
            Camera = camTransform.gameObject.AddComponent<Camera>();
            Camera.orthographic = true;
            Camera.nearClipPlane = 0.5f;
            Camera.farClipPlane = 120f;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = DarkRock;

            Quaternion tilt = Quaternion.Euler(52f, 0f, 0f);
            Vector3 focus = new Vector3(0f, 0f, -0.2f);
            camTransform.rotation = tilt;
            camTransform.position = focus - (tilt * Vector3.forward) * 28f;
            ResizeCamera();

            Light sun = Child("Duel light", root).gameObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(52f, -38f, 0f);
            sun.intensity = 0.95f;
        }

        private void ResizeCamera()
        {
            int width = Mathf.Max(Screen.width, 1);
            int height = Mathf.Max(Screen.height, 1);
            if (width == viewWidth && height == viewHeight)
                return;

            viewWidth = width;
            viewHeight = height;
            float aspect = Mathf.Max(0.5f, width / (float)height);
            Camera.orthographicSize = Mathf.Max(7.4f, 13.8f / aspect);
        }

        private Transform Child(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private Transform Model(string name, Transform parent, DuelGeometry geometry, Material material = null)
        {
            Mesh mesh = geometry.Bake(name);
            meshes.Add(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material != null ? material : fieldMaterial;
            return go.transform;
        }

        private LineRenderer Circle(string name, Transform parent, float radius, float height, float width, Color color)
        {
            const int segments = 28;
            LineRenderer line = CreateLine(name, parent, color, width, true);
            line.useWorldSpace = false;
            line.transform.localPosition = new Vector3(0f, height, 0f);
            line.positionCount = segments;
            for (int i = 0; i < segments; i++)
                line.SetPosition(i, ArcPoint(360f * i / segments) * radius);
            return line;
        }

        private LineRenderer CreateLine(string name, Transform parent, Color color, float width, bool loop)
        {
            LineRenderer line = Child(name, parent).gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = loop;
            line.sharedMaterial = lineMaterial;
            line.startColor = color;
            line.endColor = color;
            line.widthMultiplier = width;
            line.numCornerVertices = 2;
            return line;
        }

        private Material TrackMaterial(Material mat)
        {
            materials.Add(mat);
            return mat;
        }

        private static Vector3 Ground(Vector2 point, float y = 0f) => new Vector3(point.x, y, point.y);

        private static Vector3 ArcPoint(float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
        }

        public void Dispose()
        {
            foreach (Mesh mesh in meshes)
                DestroyUnityObject(mesh);
            meshes.Clear();

            foreach (Material material in materials)
                DestroyUnityObject(material);
            materials.Clear();
        }

        private static void DestroyUnityObject(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
