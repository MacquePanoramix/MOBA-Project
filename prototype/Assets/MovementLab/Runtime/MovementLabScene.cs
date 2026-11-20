using System.Collections.Generic;
using UnityEngine;

namespace Moba.MovementLab
{
    public enum MovementProfile
    {
        QuickTurn,
        DeliberateTurn
    }

    public sealed class MovementLabScene : MonoBehaviour
    {
        public static readonly Vector2 Spawn = new Vector2(-8f, -3f);
        public static readonly Vector2 Clear = new Vector2(-8f, 4f);
        public static readonly Vector2 Detour = new Vector2(8f, -3f);
        public static readonly Vector2 Return = new Vector2(-10f, -4f);
        public static readonly Vector2 Near = new Vector2(-6f, -3f);
        public static readonly Vector2 Blocked = new Vector2(0f, -3f);

        private const float HalfWidth = 12.5f;
        private const float HalfDepth = 7.5f;
        private const float TreeRadius = 0.9f;
        private const float UnitRadius = 0.46f;
        private const float SpawnHeading = 90f;

        private static readonly Color Background = new Color32(17, 26, 30, 255);
        private static readonly Color RouteColor = new Color32(109, 240, 202, 255);
        private static readonly Color RejectedColor = new Color32(255, 144, 121, 255);
        private static readonly Color StationColor = new Color32(171, 200, 207, 255);

        [SerializeField] private Shader surfaceShader;
        [SerializeField] private Shader lineShader;
        [Header("Quick turn")]
        [SerializeField] private MovementTuning baseline = new MovementTuning();
        [Header("Deliberate turn")]
        [SerializeField] private MovementTuning deliberateTurnBaseline = new MovementTuning { TurnRate = 150f };

        private readonly List<Material> materials = new List<Material>();
        private readonly Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        private Camera view;
        private Transform capsule;
        private LineRenderer routeLine;
        private Transform destinationMarker;
        private Transform rejectedMarker;
        private Material lineMaterial;
        private MovementLabHud hud;
        private MovementTuning quickTurnTuning;
        private MovementTuning deliberateTurnTuning;
        private bool hasDestination;
        private bool rejectedClick;
        private int viewWidth;
        private int viewHeight;

        public MovementCourse Course { get; private set; }
        public MovementMotor Motor { get; private set; }
        public MovementProfile ActiveProfile { get; private set; } = MovementProfile.QuickTurn;
        public MovementTuning Tuning => ActiveProfile == MovementProfile.QuickTurn ? quickTurnTuning : deliberateTurnTuning;

        private void Awake()
        {
            var trees = new[]
            {
                Blocked, new Vector2(-1.1f, -1.3f), new Vector2(1.15f, -1.3f),
                new Vector2(0f, 0.45f), new Vector2(-2.1f, 1.7f),
                new Vector2(2.1f, 1.7f), new Vector2(-0.92f, 3.45f),
                new Vector2(1.05f, 3.55f)
            };
            Course = new MovementCourse(HalfWidth, HalfDepth, trees, TreeRadius, UnitRadius);
            surfaceShader = surfaceShader != null ? surfaceShader : Shader.Find("Standard");
            lineShader = lineShader != null ? lineShader : Shader.Find("Sprites/Default");
            lineMaterial = CreateMaterial(lineShader, Color.white);
            hud = new MovementLabHud();

            CreateView();
            CreateGround();
            CreateTrees(trees);
            CreateCapsule();
            CreateStations();

            routeLine = CreateLine("Planned route", RouteColor, 0.095f, false);
            destinationMarker = CreateRing("Destination", Vector2.zero, 0.4f, 0.06f, RouteColor).transform;
            rejectedMarker = CreateRing("Rejected click", Vector2.zero, 0.52f, 0.07f, RejectedColor).transform;
            quickTurnTuning = CopyTuning(baseline);
            deliberateTurnTuning = CopyTuning(deliberateTurnBaseline);
            ResetCourse();
        }

        private void Update()
        {
            ResizeView();
            if (Input.GetKeyDown(KeyCode.R))
                ResetCourse();

            Vector3 pointer = Input.mousePosition;
            if (Input.GetMouseButtonDown(1) && view.pixelRect.Contains(pointer))
            {
                Ray ray = view.ScreenPointToRay(pointer);
                if (groundPlane.Raycast(ray, out float distance))
                {
                    Vector3 point = ray.GetPoint(distance);
                    CommandDestination(new Vector2(point.x, point.z));
                }
            }

            Motor.Tick(Time.deltaTime, Tuning);
            DrawMovement();
        }

        private void OnGUI()
        {
            hud.Draw(this, view, viewWidth, hasDestination, rejectedClick);
        }

        private void OnDestroy()
        {
            foreach (Material material in materials)
                Destroy(material);
        }

        public bool CommandDestination(Vector2 target)
        {
            bool accepted = Motor.TrySetDestination(Course, target);
            rejectedClick = !accepted;
            bool finiteTarget = Mathf.Abs(target.x) <= float.MaxValue && Mathf.Abs(target.y) <= float.MaxValue;
            rejectedMarker.gameObject.SetActive(rejectedClick && finiteTarget);
            if (accepted)
            {
                hasDestination = true;
                destinationMarker.position = AtGround(target, 0.1f);
                destinationMarker.gameObject.SetActive(true);
            }
            else if (finiteTarget)
            {
                rejectedMarker.position = AtGround(target, 1.85f);
            }
            return accepted;
        }

        public void ResetCourse()
        {
            Motor = new MovementMotor(Spawn, SpawnHeading);
            hasDestination = false;
            rejectedClick = false;
            destinationMarker.gameObject.SetActive(false);
            rejectedMarker.gameObject.SetActive(false);
            DrawMovement();
        }

        public void SelectProfile(MovementProfile profile)
        {
            if (profile != MovementProfile.QuickTurn && profile != MovementProfile.DeliberateTurn)
                throw new System.ArgumentOutOfRangeException(nameof(profile));
            if (profile == ActiveProfile)
                return;

            ActiveProfile = profile;
            ResetCourse();
        }

        public void RestoreBaseline()
        {
            if (ActiveProfile == MovementProfile.QuickTurn)
                quickTurnTuning = CopyTuning(baseline);
            else
                deliberateTurnTuning = CopyTuning(deliberateTurnBaseline);
        }

        private static MovementTuning CopyTuning(MovementTuning source)
        {
            return new MovementTuning
            {
                MaxSpeed = source.MaxSpeed,
                Acceleration = source.Acceleration,
                Deceleration = source.Deceleration,
                TurnRate = source.TurnRate,
                TurnSlowdownAngle = source.TurnSlowdownAngle
            };
        }

        private void DrawMovement()
        {
            capsule.SetPositionAndRotation(AtGround(Motor.Position), Quaternion.Euler(0f, Motor.HeadingDegrees, 0f));
            if (!Motor.IsMoving)
            {
                routeLine.positionCount = 0;
                return;
            }

            const float routeHeight = 0.12f;
            int nextWaypoint = Motor.NextWaypointIndex;
            var route = Motor.Route;
            routeLine.positionCount = route.Count - nextWaypoint + 1;
            routeLine.SetPosition(0, AtGround(Motor.Position, routeHeight));
            for (int i = nextWaypoint; i < route.Count; i++)
                routeLine.SetPosition(i - nextWaypoint + 1, AtGround(route[i], routeHeight));
        }

        private void CreateView()
        {
            view = CreateChild("Course camera", transform).gameObject.AddComponent<Camera>();
            view.transform.SetPositionAndRotation(new Vector3(0f, 24f, 0f), Quaternion.Euler(90f, 0f, 0f));
            view.orthographic = true;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = Background;
            ResizeView();

            Light sun = CreateChild("Course light", transform).gameObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(52f, -40f, 0f);
            sun.intensity = 1.1f;
        }

        private void ResizeView()
        {
            int width = Mathf.Max(Screen.width, 1);
            int height = Mathf.Max(Screen.height, 1);
            if (width == viewWidth && height == viewHeight)
                return;

            viewWidth = width;
            viewHeight = height;
            float courseWidth = width - MovementLabHud.PanelWidth(width);
            view.rect = new Rect(0f, 0f, courseWidth / width, 1f);
            view.orthographicSize = Mathf.Max(HalfDepth + 2.1f, (HalfWidth + 1.15f) * height / courseWidth);
        }

        private void CreateGround()
        {
            Material ground = CreateMaterial(surfaceShader, new Color32(57, 79, 81, 255));
            Material rim = CreateMaterial(surfaceShader, new Color32(83, 108, 105, 255));
            Material grid = CreateMaterial(surfaceShader, new Color32(64, 87, 88, 255));
            CreatePrimitive(PrimitiveType.Cube, "Course platform", new Vector3(0f, -0.31f, 0f),
                new Vector3(HalfWidth * 2f + 0.46f, 0.4f, HalfDepth * 2f + 0.46f), rim, transform);
            CreatePrimitive(PrimitiveType.Cube, "Walkable ground", new Vector3(0f, -0.16f, 0f),
                new Vector3(HalfWidth * 2f, 0.32f, HalfDepth * 2f), ground, transform);
            for (int x = -12; x <= 12; x += 2)
                CreatePrimitive(PrimitiveType.Cube, "Grid line", new Vector3(x, 0f, 0f),
                    new Vector3(0.024f, 0.009f, HalfDepth * 2f), grid, transform);
            for (int z = -6; z <= 6; z += 2)
                CreatePrimitive(PrimitiveType.Cube, "Grid line", new Vector3(0f, 0.001f, z),
                    new Vector3(HalfWidth * 2f, 0.009f, 0.024f), grid, transform);
        }

        private void CreateTrees(Vector2[] trees)
        {
            Material baseMaterial = CreateMaterial(surfaceShader, new Color32(39, 59, 60, 255));
            Material canopy = CreateMaterial(surfaceShader, new Color32(43, 119, 97, 255));
            Material crown = CreateMaterial(surfaceShader, new Color32(73, 149, 112, 255));
            Transform grove = CreateChild("Static grove", transform);
            for (int i = 0; i < trees.Length; i++)
            {
                Transform tree = CreateChild($"Tree {i + 1}", grove);
                tree.localPosition = AtGround(trees[i]);
                float diameter = TreeRadius * 2f;
                CreatePrimitive(PrimitiveType.Cylinder, "Tree footprint", new Vector3(0f, 0.035f, 0f),
                    new Vector3(diameter, 0.035f, diameter), baseMaterial, tree);
                CreatePrimitive(PrimitiveType.Sphere, "Tree canopy", new Vector3(0f, 0.72f, 0f),
                    new Vector3(diameter * 0.92f, 1.3f, diameter * 0.92f), canopy, tree);
                CreatePrimitive(PrimitiveType.Sphere, "Tree crown", new Vector3(0.105f, 1.22f, 0.13f),
                    new Vector3(0.9f, 0.75f, 0.9f), crown, tree);
            }
        }

        private void CreateCapsule()
        {
            Material body = CreateMaterial(surfaceShader, new Color32(232, 244, 234, 255));
            Material facing = CreateMaterial(surfaceShader, new Color32(23, 89, 84, 255));
            capsule = CreateChild("Player capsule", transform);
            float diameter = UnitRadius * 2f;
            CreatePrimitive(PrimitiveType.Capsule, "Body", new Vector3(0f, 0.68f, 0f),
                new Vector3(diameter, 0.68f, diameter), body, capsule);
            Transform left = CreatePrimitive(PrimitiveType.Cube, "Facing arrow", new Vector3(-0.105f, 1.37f, 0.1f),
                new Vector3(0.08f, 0.04f, 0.32f), facing, capsule);
            Transform right = CreatePrimitive(PrimitiveType.Cube, "Facing arrow", new Vector3(0.105f, 1.37f, 0.1f),
                new Vector3(0.08f, 0.04f, 0.32f), facing, capsule);
            left.localRotation = Quaternion.Euler(0f, 48f, 0f);
            right.localRotation = Quaternion.Euler(0f, -48f, 0f);
        }

        private void CreateStations()
        {
            CreateRing("Spawn station", Spawn, 0.63f, 0.045f, StationColor);
            CreateRing("Open ground station", Clear, 0.28f, 0.045f, StationColor);
            CreateRing("Detour station", Detour, 0.28f, 0.045f, StationColor);
            CreateRing("Return station", Return, 0.28f, 0.045f, StationColor);
            CreateRing("Near station", Near, 0.28f, 0.045f, StationColor);
            LineRenderer blocked = CreateRing("Blocked station", Blocked, 0.27f, 0.045f, RejectedColor);
            blocked.transform.localPosition = AtGround(Blocked, 1.85f);
        }

        private LineRenderer CreateRing(string objectName, Vector2 position, float radius, float width, Color color)
        {
            const int segments = 28;
            LineRenderer ring = CreateLine(objectName, color, width, true);
            ring.transform.localPosition = AtGround(position, 0.07f);
            ring.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
            return ring;
        }

        private LineRenderer CreateLine(string objectName, Color color, float width, bool loop)
        {
            LineRenderer line = CreateChild(objectName, transform).gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = !loop;
            line.loop = loop;
            line.sharedMaterial = lineMaterial;
            line.startColor = color;
            line.endColor = color;
            line.widthMultiplier = width;
            line.numCornerVertices = 3;
            return line;
        }

        private Material CreateMaterial(Shader shader, Color color)
        {
            var material = new Material(shader) { color = color };
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", 0.18f);
            materials.Add(material);
            return material;
        }

        private static Transform CreateChild(string objectName, Transform parent)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Transform CreatePrimitive(PrimitiveType shape, string objectName, Vector3 position,
            Vector3 scale, Material material, Transform parent)
        {
            GameObject primitive = GameObject.CreatePrimitive(shape);
            primitive.name = objectName;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = position;
            primitive.transform.localScale = scale;
            primitive.GetComponent<MeshRenderer>().sharedMaterial = material;
            Destroy(primitive.GetComponent<Collider>());
            return primitive.transform;
        }

        private static Vector3 AtGround(Vector2 point, float height = 0f)
        {
            return new Vector3(point.x, height, point.y);
        }
    }
}
