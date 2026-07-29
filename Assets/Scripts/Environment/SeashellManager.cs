using UnityEngine;

// Tracks seashell collectibles found during exploration. This is deliberately
// minimal for now - a runtime counter plus a hook other systems can subscribe
// to - since the skill tree it will eventually unlock progression in doesn't
// exist yet. No persistence (PlayerPrefs/save file) is wired up yet either;
// add that here later without needing to touch SeashellCollectible.
public class SeashellManager : MonoBehaviour
{
    public static SeashellManager Instance { get; private set; }

    public int TotalCollected { get; private set; }

    // Subscribe from UI / future skill tree unlock logic. Passes the new
    // running total after each pickup.
    public event System.Action<int> OnShellCollected;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void CollectShell(SeashellCollectible shell)
    {
        TotalCollected++;
        Debug.Log("[SeashellManager] Shell collected (" + (shell != null ? shell.name : "unknown") + "). Total: " + TotalCollected);
        OnShellCollected?.Invoke(TotalCollected);
    }
}
