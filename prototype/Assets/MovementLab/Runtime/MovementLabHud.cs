using UnityEngine;

namespace Moba.MovementLab
{
    internal sealed class MovementLabHud
    {
        private static readonly Color Panel = new Color32(23, 35, 40, 255);
        private static readonly Color Divider = new Color32(53, 75, 81, 255);
        private static readonly Color Primary = new Color32(235, 246, 245, 255);
        private static readonly Color Secondary = new Color32(181, 205, 210, 255);
        private static readonly Color Accent = new Color32(109, 240, 202, 255);
        private static readonly Color Warning = new Color32(255, 144, 121, 255);

        private GUIStyle title;
        private GUIStyle heading;
        private GUIStyle body;
        private GUIStyle value;
        private GUIStyle station;
        private GUIStyle button;
        private Vector2 tuningScroll;

        public static float PanelWidth(int screenWidth)
        {
            return Mathf.Min(Mathf.Clamp(screenWidth * 0.22f, 224f, 290f), screenWidth * 0.44f);
        }

        public void Draw(MovementLabScene scene, Camera view, int screenWidth, bool hasDestination, bool rejectedClick)
        {
            if (title == null)
                CreateStyles();

            DrawStation(view, MovementLabScene.Spawn, "Spawn", 54f, -28f);
            DrawStation(view, MovementLabScene.Clear, "Open", 46f, -28f);
            DrawStation(view, MovementLabScene.Detour, "Detour", 54f, -28f);
            DrawStation(view, MovementLabScene.Return, "Return", 54f, 9f);
            DrawStation(view, MovementLabScene.Near, "Near", 46f, 9f);
            DrawStation(view, MovementLabScene.Blocked, "Blocked", 66f, 13f);

            GUI.Label(new Rect(20f, 14f, 250f, 32f), "Movement Lab", title);
            GUI.Label(new Rect(20f, 47f, 265f, 22f), "Right-click to move   /   R to restart", body);

            float width = PanelWidth(screenWidth);
            float x = screenWidth - width;
            Fill(new Rect(x, 0f, width, Screen.height), Panel);
            Fill(new Rect(x, 0f, 1f, Screen.height), Divider);
            var content = new Rect(x + 16f, 14f, width - 32f, Screen.height - 28f);
            GUILayout.BeginArea(content);
            DrawProfiles(scene, content.width);

            GUILayout.Space(8f);
            tuningScroll = GUILayout.BeginScrollView(tuningScroll);
            DrawStatus(scene.Motor, hasDestination, rejectedClick);

            GUILayout.Space(8f);
            GUILayout.Label("Prototype tuning", heading);
            MovementTuning tuning = scene.Tuning;
            tuning.MaxSpeed = Slider("Move speed", tuning.MaxSpeed, 1f, 10f, $"{tuning.MaxSpeed:0.0} u/s");
            tuning.Acceleration = Slider("Acceleration", tuning.Acceleration, 1f, 24f, $"{tuning.Acceleration:0.0} u/s²");
            tuning.Deceleration = Slider("Braking", tuning.Deceleration, 1f, 28f, $"{tuning.Deceleration:0.0} u/s²");
            tuning.TurnRate = Slider("Turn rate", tuning.TurnRate, 45f, 540f, $"{tuning.TurnRate:0}°/s");
            tuning.TurnSlowdownAngle = Slider("Turn slowdown angle", tuning.TurnSlowdownAngle, 15f, 90f,
                $"{tuning.TurnSlowdownAngle:0}°");

            GUILayout.Space(7f);
            if (GUILayout.Button("Restore baseline", button, GUILayout.Height(30f)))
                scene.RestoreBaseline();
            if (GUILayout.Button("Reset course", button, GUILayout.Height(30f)))
                scene.ResetCourse();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawProfiles(MovementLabScene scene, float width)
        {
            GUILayout.Label("Unit profile", heading);
            bool sideBySide = width >= 174f;
            if (sideBySide)
                GUILayout.BeginHorizontal();

            DrawProfile(scene, MovementProfile.QuickTurn, "Quick turn");
            DrawProfile(scene, MovementProfile.DeliberateTurn, "Deliberate turn");

            if (sideBySide)
                GUILayout.EndHorizontal();
        }

        private void DrawProfile(MovementLabScene scene, MovementProfile profile, string label)
        {
            bool selected = scene.ActiveProfile == profile;
            Color priorColor = GUI.backgroundColor;
            if (selected)
                GUI.backgroundColor = Accent;
            if (GUILayout.Toggle(selected, label, button, GUILayout.Height(28f)) && !selected)
                scene.SelectProfile(profile);
            GUI.backgroundColor = priorColor;
        }

        private void DrawStatus(MovementMotor motor, bool hasDestination, bool rejectedClick)
        {
            GUILayout.Label("Movement status", heading);
            string status = "Ready";
            if (motor.IsMoving)
                status = "Following route";
            else if (hasDestination)
                status = "Arrived";

            Color priorColor = GUI.color;
            GUI.color = rejectedClick ? Warning : Accent;
            GUILayout.Label(rejectedClick ? "Blocked click" : status, heading);
            GUI.color = priorColor;

            GUILayout.Label($"{motor.Speed:0.00} u/s    {Mathf.Repeat(motor.HeadingDegrees, 360f):0}° facing", body);
            GUILayout.Label(rejectedClick && motor.IsMoving ? "Following the previous route" : " ", body);
        }

        private float Slider(string label, float input, float minimum, float maximum, string displayValue)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, body);
            GUILayout.FlexibleSpace();
            GUILayout.Label(displayValue, value);
            GUILayout.EndHorizontal();
            float result = GUILayout.HorizontalSlider(input, minimum, maximum, GUILayout.Height(17f));
            GUILayout.Space(7f);
            return result;
        }

        private void DrawStation(Camera view, Vector2 location, string label, float width, float offset)
        {
            Vector3 position = view.WorldToScreenPoint(new Vector3(location.x, 0f, location.y));
            var rect = new Rect(position.x - width / 2f, Screen.height - position.y + offset, width, 19f);
            Fill(rect, Panel);
            GUI.Label(rect, label, station);
        }

        private void CreateStyles()
        {
            title = new GUIStyle(GUI.skin.label) { fontSize = 23, fontStyle = FontStyle.Bold };
            title.normal.textColor = Primary;
            heading = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            heading.normal.textColor = Color.white;
            body = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            body.normal.textColor = Secondary;
            value = new GUIStyle(body) { alignment = TextAnchor.MiddleRight };
            value.normal.textColor = Primary;
            station = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter };
            station.normal.textColor = Primary;
            button = new GUIStyle(GUI.skin.button) { fontSize = 13 };
        }

        private static void Fill(Rect rect, Color color)
        {
            Color priorColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = priorColor;
        }
    }
}
