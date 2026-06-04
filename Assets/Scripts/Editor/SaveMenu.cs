#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BlockJam.EditorTools
{
    /// <summary>
    /// Editor menu commands for managing the player's save file during development.
    /// The save lives at Application.persistentDataPath/blockjam_save.json (see SaveSystem).
    /// "Reset Save" deletes it so the next Play starts fresh from the first level.
    /// </summary>
    public static class SaveMenu
    {
        private const string FileName = "blockjam_save.json";

        private static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        [MenuItem("Tools/BlockJam/Reset Save (baştan başla)")]
        public static void ResetSave()
        {
            string path = SavePath;
            if (File.Exists(path))
            {
                File.Delete(path);
                Debug.Log("[BlockJam] Save silindi, oyun bastan baslayacak: " + path);
            }
            else
            {
                Debug.Log("[BlockJam] Save dosyasi zaten yok (zaten bastan baslayacak): " + path);
            }
        }

        [MenuItem("Tools/BlockJam/Save Yolunu Goster")]
        public static void ShowSavePath()
        {
            Debug.Log("[BlockJam] Save dosyasi konumu: " + SavePath +
                      " | Var mi? " + File.Exists(SavePath));
        }
    }
}
#endif
