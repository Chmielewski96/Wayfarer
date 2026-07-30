using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Wayfarer.UI
{
    /// <summary>
    /// Quest journal - toggled with J (a self-contained menu key, same direct-Keyboard-polling
    /// pattern as SeashellCollectible's F and IceSurfController's Q boost, rather than a shared
    /// input action). Lists every quest QuestManager knows about on the left; number keys
    /// (1-9) pick which one's description shows on the right. Freezes player movement/actions
    /// while open via PlayerController.SetMenuOpen, the same mechanism NPCInteractable uses
    /// for conversations, so surf/jump/spell-arming can't sneak in behind an open menu the way
    /// they briefly could behind dialogue before that was fixed.
    /// </summary>
    public class JournalUI : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text[] questListRows;
        [SerializeField] private Text detailTitleText;
        [SerializeField] private Text detailBodyText;
        [SerializeField] private Text emptyStateText;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private Key toggleKey = Key.J;
        [SerializeField] private Key[] selectKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };

        [Header("Row Colors")]
        [SerializeField] private Color selectedColor = Color.white;
        [SerializeField] private Color unselectedColor = new Color(1f, 1f, 1f, 0.6f);

        [Header("Fade")]
        [Tooltip("Optional - auto-added to panelRoot if missing. Drives the open/close fade.")]
        [SerializeField] private CanvasGroup panelCanvasGroup;
        [SerializeField] private float fadeDuration = 0.2f;

        private int selectedIndex;
        private int openedFrame = -1;
        private Coroutine fadeCoroutine;
        private bool isFading;

        public static JournalUI Instance { get; private set; }

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        private void Awake()
        {
            Instance = this;

            if (playerController == null)
            {
                playerController = FindFirstObjectByType<PlayerController>();
            }

            if (panelRoot != null)
            {
                panelCanvasGroup = panelRoot.GetComponent<CanvasGroup>();
                if (panelCanvasGroup == null) { panelCanvasGroup = panelRoot.AddComponent<CanvasGroup>(); }
                panelCanvasGroup.alpha = 0f;
                panelRoot.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.OnQuestsChanged += HandleQuestsChanged;
            }
        }

        private void OnDisable()
        {
            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.OnQuestsChanged -= HandleQuestsChanged;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) { Instance = null; }
        }

        private void HandleQuestsChanged()
        {
            if (IsOpen)
            {
                RefreshList();
            }
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current[toggleKey].wasPressedThisFrame && !isFading)
            {
                // Mutually exclusive with talking to an NPC - both freeze the player and take
                // over the screen, and opening one mid-the-other would leave state (camera
                // lock, dialogue box, frozen movement) in a confusing overlap.
                bool blockedByDialogue = DialogueUI.Instance != null && DialogueUI.Instance.IsOpen;
                if (!IsOpen && blockedByDialogue) return;

                if (IsOpen) Close();
                else Open();
            }

            if (!IsOpen || isFading) return;
            if (Time.frameCount == openedFrame) return;

            for (int i = 0; i < selectKeys.Length; i++)
            {
                if (Keyboard.current[selectKeys[i]].wasPressedThisFrame)
                {
                    SelectIndex(i);
                    break;
                }
            }
        }

        private void Open()
        {
            openedFrame = Time.frameCount;
            selectedIndex = 0;

            if (playerController != null)
            {
                playerController.SetMenuOpen(true);
            }

            RefreshList();

            if (panelRoot != null) { panelRoot.SetActive(true); }
            StartFade(1f, deactivateOnComplete: false);
        }

        private void Close()
        {
            if (playerController != null)
            {
                playerController.SetMenuOpen(false);
            }

            StartFade(0f, deactivateOnComplete: true);
        }

        private void StartFade(float targetAlpha, bool deactivateOnComplete)
        {
            if (panelCanvasGroup == null)
            {
                if (deactivateOnComplete && panelRoot != null) { panelRoot.SetActive(false); }
                return;
            }

            if (fadeCoroutine != null) { StopCoroutine(fadeCoroutine); }
            fadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha, deactivateOnComplete));
        }

        // Runs on unscaled time so the fade still plays at a consistent speed even if
        // Time.timeScale is ever changed for a future pause feature. Input is locked out for
        // the duration (see the isFading checks in Update()) - simplest way to avoid an
        // overlapping open/close fade fighting over the same CanvasGroup.
        private IEnumerator FadeRoutine(float targetAlpha, bool deactivateOnComplete)
        {
            isFading = true;
            float startAlpha = panelCanvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = fadeDuration > 0.0001f ? Mathf.Clamp01(elapsed / fadeDuration) : 1f;
                panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            panelCanvasGroup.alpha = targetAlpha;
            isFading = false;
            fadeCoroutine = null;

            if (deactivateOnComplete && panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void SelectIndex(int index)
        {
            var quests = QuestManager.Instance != null ? QuestManager.Instance.Quests : null;
            if (quests == null || index < 0 || index >= quests.Count) return;

            selectedIndex = index;
            RenderRows();
            RenderDetail();
        }

        private void RefreshList()
        {
            var quests = QuestManager.Instance != null ? QuestManager.Instance.Quests : null;
            int count = quests != null ? quests.Count : 0;

            if (selectedIndex >= count) { selectedIndex = Mathf.Max(0, count - 1); }

            if (emptyStateText != null) { emptyStateText.gameObject.SetActive(count == 0); }

            RenderRows();
            RenderDetail();
        }

        private void RenderRows()
        {
            var quests = QuestManager.Instance != null ? QuestManager.Instance.Quests : null;
            int count = quests != null ? quests.Count : 0;

            for (int i = 0; i < questListRows.Length; i++)
            {
                if (questListRows[i] == null) continue;

                if (i < count)
                {
                    var q = quests[i];
                    string statusSuffix = q.status == QuestManager.Status.Completed ? "  (Completed)" : "  (In Progress)";
                    questListRows[i].text = (i + 1) + ". " + q.title + statusSuffix;
                    questListRows[i].gameObject.SetActive(true);
                    questListRows[i].color = i == selectedIndex ? selectedColor : unselectedColor;
                }
                else
                {
                    questListRows[i].gameObject.SetActive(false);
                }
            }
        }

        private void RenderDetail()
        {
            var quests = QuestManager.Instance != null ? QuestManager.Instance.Quests : null;
            int count = quests != null ? quests.Count : 0;

            if (count == 0)
            {
                if (detailTitleText != null) { detailTitleText.text = string.Empty; }
                if (detailBodyText != null) { detailBodyText.text = string.Empty; }
                return;
            }

            var selected = quests[selectedIndex];
            if (detailTitleText != null)
            {
                string statusSuffix = selected.status == QuestManager.Status.Completed ? " (Completed)" : " (In Progress)";
                detailTitleText.text = selected.title + statusSuffix;
            }
            if (detailBodyText != null) { detailBodyText.text = selected.description; }
        }
    }
}
