using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Moba.MovementLab
{
    public static class MovementLabEditor
    {
        public const string ScenePath = "Assets/MovementLab/Scenes/MovementLab.unity";

        // Used for the initial scene and for creating a replacement if the asset is missing.
        public static void CreateFirstScene()
        {
            if (File.Exists(ScenePath))
                throw new InvalidOperationException($"The lab scene already exists at {ScenePath}.");

            var surfaceShader = Shader.Find("Standard");
            var lineShader = Shader.Find("Sprites/Default");
            if (surfaceShader == null || lineShader == null)
                throw new InvalidOperationException("The built-in lab shaders could not be loaded.");

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var lab = new GameObject("Movement Lab").AddComponent<MovementLabScene>();
            var config = new SerializedObject(lab);
            config.FindProperty("surfaceShader").objectReferenceValue = surfaceShader;
            config.FindProperty("lineShader").objectReferenceValue = lineShader;
            config.ApplyModifiedPropertiesWithoutUndo();

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException($"Could not save {ScenePath}.");

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.productName = "Movement Lab";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.resizableWindow = true;
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Movement Lab/Build Windows Player")]
        public static void BuildWindows()
        {
            const string output = "Builds/Windows/MovementLab.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, output,
                BuildTarget.StandaloneWindows64, BuildOptions.None);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Windows build {report.summary.result} with {report.summary.totalErrors} errors.");

            Debug.Log($"Movement Lab Windows build succeeded: {report.summary.outputPath}");
        }
    }
}
