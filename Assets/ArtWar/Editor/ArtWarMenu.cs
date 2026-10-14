using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ArtWar.EditorTools
{
    public static class ArtWarMenu
    {
        const string ScenePath = "Assets/ArtWar/Scenes/Prototype.unity";

        [MenuItem("Art War/Create Prototype Scene")]
        public static void CreatePrototypeScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Execute();
        }

        /// <summary>Creates (or recreates) the prototype scene and puts it first in Build Settings.</summary>
        public static void Execute()
        {
            System.IO.Directory.CreateDirectory("Assets/ArtWar/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Art War").AddComponent<BattleManager>();
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("Art War prototype scene created at " + ScenePath);
        }
    }
}
