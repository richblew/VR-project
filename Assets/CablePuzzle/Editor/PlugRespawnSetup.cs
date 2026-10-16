using UnityEditor;
using UnityEngine;

namespace CablePuzzle.EditorTools
{
    /// <summary>
    /// Tools > Cable Puzzle > Add Respawn To All Plugs
    /// Adds a PlugRespawn component to every CableConnector in the open scene that is marked "Is Plug". Undoable.
    /// Nothing else in the scene is touched.
    /// </summary>
    public static class PlugRespawnSetup
    {
        [MenuItem("Tools/Cable Puzzle/Add Respawn To All Plugs")]
        public static void AddToPlugs()
        {
            int added = 0, existing = 0, plugs = 0;
            foreach (var c in Object.FindObjectsByType<CableConnector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!c.isPlug) continue;
                plugs++;
                if (c.GetComponent<PlugRespawn>() != null) { existing++; continue; }
                Undo.AddComponent<PlugRespawn>(c.gameObject);
                added++;
            }
            Debug.Log(plugs == 0
                ? "Cable Puzzle: no CableConnector with 'Is Plug' ticked found in the open scene."
                : $"Cable Puzzle: PlugRespawn added to {added} plug(s); {existing} already had it. Save the scene to keep it.");
        }
    }
}
