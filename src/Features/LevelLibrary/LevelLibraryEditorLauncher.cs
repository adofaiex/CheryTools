using System.Collections;
using UnityEngine;

namespace CheryTools
{
    internal static class LevelLibraryEditorLauncher
    {
        private static LevelLibraryEditorLauncherRunner _runner;

        internal static void Open(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            EnsureRunner();
            _runner.StartEditorLoad(path);
        }

        private static void EnsureRunner()
        {
            if (_runner != null) return;
            GameObject go = new GameObject("CheryTools_LevelLibraryEditorLauncher");
            Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<LevelLibraryEditorLauncherRunner>();
        }
    }

    internal sealed class LevelLibraryEditorLauncherRunner : MonoBehaviour
    {
        internal void StartEditorLoad(string path)
        {
            StopAllCoroutines();
            StartCoroutine(LoadEditorLevel(path));
        }

        private IEnumerator LoadEditorLevel(string path)
        {
            LevelSelectBase levelSelect = Object.FindObjectOfType<LevelSelectBase>();
            if (levelSelect == null)
            {
                Main.Logger?.Log("[CheryTools] Failed to find level select while opening library level.");
                yield break;
            }

            levelSelect.GoToLevelEditor();

            scnEditor editor = null;
            for (int i = 0; i < 600; i++)
            {
                editor = Object.FindObjectOfType<scnEditor>();
                if (editor != null && editor.levelData != null)
                    break;
                yield return null;
            }

            if (editor == null)
            {
                Main.Logger?.Log("[CheryTools] Failed to find scnEditor while opening library level.");
                yield break;
            }

            editor.OpenLevel(path);
        }
    }
}
