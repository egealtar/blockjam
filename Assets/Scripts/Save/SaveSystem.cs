using System.IO;
using UnityEngine;

namespace BlockJam.Save
{
    /// <summary>
    /// Persists progress to disk as JSON under Application.persistentDataPath.
    ///
    /// Why a real file (not PlayerPrefs):
    ///  - The case explicitly says "persisted locally on disk and restored when the game restarts".
    ///  - A JSON file is inspectable, portable, and works identically in Editor and on Android.
    ///
    /// This is a plain class (not a MonoBehaviour, not a singleton). The owner - GameController -
    /// creates one instance and holds it. That keeps the dependency explicit and testable.
    /// Writes are atomic (write to a temp file, then replace) so a crash mid-write can't corrupt the save.
    /// </summary>
    public class SaveSystem
    {
        private const string FileName = "blockjam_save.json";

        private readonly string _path;
        private SaveData _cached;

        public SaveSystem()
        {
            _path = System.IO.Path.Combine(Application.persistentDataPath, FileName);        }

        /// <summary>Full path of the save file (handy for logging / debugging on device).</summary>
        public string Path => _path;

        /// <summary>
        /// Loads the save from disk, or returns a fresh default if none exists / it's unreadable.
        /// Result is cached so repeated calls are cheap.
        /// </summary>
        public SaveData Load()
        {
            if (_cached != null) return _cached;

            try
            {
                if (File.Exists(_path))
                {
                    string json = File.ReadAllText(_path);
                    var data = JsonUtility.FromJson<SaveData>(json);
                    if (data != null)
                    {
                        _cached = data;
                        return _cached;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Failed to load save, starting fresh. {e.Message}");
            }

            _cached = new SaveData();
            return _cached;
        }

        /// <summary>Persists the given data to disk atomically.</summary>
        public void Save(SaveData data)
        {
            if (data == null) return;
            _cached = data;

            try
            {
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                string tmp = _path + ".tmp";
                File.WriteAllText(tmp, json);

                if (File.Exists(_path)) File.Delete(_path);
                File.Move(tmp, _path);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveSystem] Failed to write save. {e.Message}");
            }
        }

        /// <summary>Convenience: update the current level index and immediately persist.</summary>
        public void SaveLevelProgress(int currentLevelIndex)
        {
            var data = Load();
            data.CurrentLevelIndex = currentLevelIndex;
            if (currentLevelIndex > data.HighestLevelReached)
                data.HighestLevelReached = currentLevelIndex;
            Save(data);
        }

#if UNITY_EDITOR
        /// <summary>Editor-only helper to wipe progress while testing.</summary>
        public void DeleteSave()
        {
            _cached = null;
            if (File.Exists(_path)) File.Delete(_path);
        }
#endif
    }
}
