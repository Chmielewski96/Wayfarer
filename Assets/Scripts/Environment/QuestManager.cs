using System;
using System.Collections.Generic;
using UnityEngine;

// Tracks quests the player has accepted, for the Journal UI to list. Deliberately minimal -
// same spirit as SeashellManager (a runtime list plus a change event, no persistence yet) -
// since this is a placeholder system built to prove out the journal UI and one NPC's fetch
// quest, not a full quest-content pipeline.
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    public enum Status { InProgress, Completed }

    [Serializable]
    public class QuestEntry
    {
        public string id;
        public string title;
        [TextArea(2, 5)] public string description;
        public Status status;
    }

    private readonly List<QuestEntry> quests = new List<QuestEntry>();
    public IReadOnlyList<QuestEntry> Quests => quests;

    // Fired whenever a quest is added or an existing one's description/status changes, so the
    // Journal UI can refresh without polling every frame.
    public event Action OnQuestsChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // Quests are identified by a stable string id (e.g. "villager_seashell") rather than an
    // index, since NPCs call this repeatedly as their quest's state/description text changes
    // over a conversation (accepted -> in progress -> completed) and need to update the same
    // journal entry in place rather than adding duplicates.
    public void AddOrUpdateQuest(string id, string title, string description, Status status)
    {
        QuestEntry existing = quests.Find(q => q.id == id);
        if (existing != null)
        {
            existing.title = title;
            existing.description = description;
            existing.status = status;
        }
        else
        {
            quests.Add(new QuestEntry { id = id, title = title, description = description, status = status });
        }

        OnQuestsChanged?.Invoke();
    }
}
