using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Wayfarer.UI
{
    /// <summary>
    /// Screen-space dialogue box, shared by every NPCInteractable in the scene (only one
    /// conversation can be open at a time, so a single reusable panel + singleton is simpler
    /// than each NPC owning its own UI). Shows one line at a time; pressing the interact key
    /// (F, matching the seashell pickup binding) advances to the next line, or closes the box
    /// on the last line. Uses direct Keyboard polling for that key, same self-contained
    /// approach SeashellCollectible and IceSurfController's Q boost already use rather than
    /// wiring a new action into the shared input asset.
    ///
    /// Input handling lives here (not in NPCInteractable) because only one conversation is ever
    /// open at once - once Open() is called, every subsequent F press until Close() belongs to
    /// "advance this dialogue", regardless of which NPC started it.
    /// </summary>
    public class DialogueUI : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text speakerNameText;
        [SerializeField] private Text bodyText;
        [SerializeField] private Text continueHintText;
        [SerializeField] private Key advanceKey = Key.F;

        public static DialogueUI Instance { get; private set; }

        private string[] lines;
        private int lineIndex;
        private Action onClosed;
        private int openedFrame = -1;

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        private void Awake()
        {
            Instance = this;
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Open(string speakerName, string[] dialogueLines, Action onClosedCallback)
        {
            if (dialogueLines == null || dialogueLines.Length == 0) return;

            lines = dialogueLines;
            lineIndex = 0;
            onClosed = onClosedCallback;
            // Recorded so Update() can ignore the very same F press that called Open() this
            // frame - without this, the key that started the conversation would immediately
            // also register as "advance" the instant this box opens, since
            // wasPressedThisFrame stays true for the whole frame it was pressed on.
            openedFrame = Time.frameCount;

            if (speakerNameText != null) { speakerNameText.text = speakerName; }
            RenderCurrentLine();

            if (panelRoot != null) { panelRoot.SetActive(true); }
        }

        private void Update()
        {
            if (!IsOpen) return;
            if (Time.frameCount == openedFrame) return;

            if (Keyboard.current != null && Keyboard.current[advanceKey].wasPressedThisFrame)
            {
                Advance();
            }
        }

        private void Advance()
        {
            lineIndex++;
            if (lineIndex >= lines.Length)
            {
                Close();
            }
            else
            {
                RenderCurrentLine();
            }
        }

        private void RenderCurrentLine()
        {
            if (bodyText != null) { bodyText.text = lines[lineIndex]; }
            if (continueHintText != null)
            {
                bool isLast = lineIndex >= lines.Length - 1;
                continueHintText.text = isLast ? "F - Close" : "F - Continue";
            }
        }

        private void Close()
        {
            if (panelRoot != null) { panelRoot.SetActive(false); }
            lines = null;
            Action callback = onClosed;
            onClosed = null;
            callback?.Invoke();
        }
    }
}
