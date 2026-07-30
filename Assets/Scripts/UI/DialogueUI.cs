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
    /// (F, matching the seashell pickup binding) advances to the next line. If the current
    /// step has choices attached, reaching its last line shows a numbered list instead of
    /// closing - pressing the matching number key (1, 2, 3...) picks one, which fires that
    /// choice's callback and hands control back to whatever set up the conversation (an
    /// NPCInteractable's quest logic, typically) to decide what happens next.
    ///
    /// Input handling lives here (not in NPCInteractable) because only one conversation is
    /// ever open at once - once Open() is called, every subsequent F/number-key press until
    /// Close() belongs to "this dialogue", regardless of which NPC started it.
    /// </summary>
    public class DialogueUI : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text speakerNameText;
        [SerializeField] private Text bodyText;
        [SerializeField] private Text continueHintText;
        [SerializeField] private Key advanceKey = Key.F;
        [Tooltip("Number keys used to pick a dialogue choice, in order (matches choice index 0, 1, 2...).")]
        [SerializeField] private Key[] choiceKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };

        public static DialogueUI Instance { get; private set; }

        // A single option shown after a line sequence finishes - Label is what's displayed,
        // OnChosen fires the instant it's picked. OnChosen is typically a closure that updates
        // some quest/relationship state and then calls ShowLines() again to continue the same
        // conversation with a reaction line, or does nothing further to let it close naturally.
        public struct DialogueChoice
        {
            public string Label;
            public Action OnChosen;
        }

        private string[] lines;
        private int lineIndex;
        private DialogueChoice[] pendingChoices;
        private bool awaitingChoice;
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

        // Starts a brand new conversation - the onClosed callback fires once the WHOLE
        // conversation truly ends (the player pressed through a final choiceless line, or
        // picked a choice whose OnChosen didn't continue it), not on every intermediate step.
        // See ShowLines for continuing an already-open conversation without ending it early.
        public void Open(string speakerName, string[] dialogueLines, DialogueChoice[] choices, Action onClosedCallback)
        {
            if (dialogueLines == null || dialogueLines.Length == 0) return;

            onClosed = onClosedCallback;
            // Recorded so Update() can ignore the very same key press that called Open() this
            // frame - without this, the key that started the conversation would immediately
            // also register as "advance" the instant this box opens, since
            // wasPressedThisFrame stays true for the whole frame it was pressed on.
            openedFrame = Time.frameCount;

            if (speakerNameText != null) { speakerNameText.text = speakerName; }

            ShowLines(dialogueLines, choices);

            if (panelRoot != null) { panelRoot.SetActive(true); }
        }

        // Convenience overload for simple, choice-less NPCs.
        public void Open(string speakerName, string[] dialogueLines, Action onClosedCallback)
        {
            Open(speakerName, dialogueLines, null, onClosedCallback);
        }

        // Continues the CURRENTLY open conversation with a new batch of lines (and optionally
        // a fresh set of choices once those finish) - used from inside a choice's OnChosen to
        // move to the next step of the same conversation. Deliberately does not touch
        // onClosed or panelRoot's active state, since the conversation isn't ending here.
        public void ShowLines(string[] dialogueLines, DialogueChoice[] choices = null)
        {
            lines = dialogueLines;
            lineIndex = 0;
            pendingChoices = choices;
            awaitingChoice = false;
            RenderCurrentLine();
        }

        private void Update()
        {
            if (!IsOpen) return;
            if (Time.frameCount == openedFrame) return;
            if (Keyboard.current == null) return;

            if (awaitingChoice)
            {
                if (pendingChoices == null) return;

                for (int i = 0; i < pendingChoices.Length && i < choiceKeys.Length; i++)
                {
                    if (Keyboard.current[choiceKeys[i]].wasPressedThisFrame)
                    {
                        DialogueChoice chosen = pendingChoices[i];
                        pendingChoices = null;
                        awaitingChoice = false;
                        chosen.OnChosen?.Invoke();
                        return;
                    }
                }
                return;
            }

            if (Keyboard.current[advanceKey].wasPressedThisFrame)
            {
                Advance();
            }
        }

        private void Advance()
        {
            lineIndex++;
            if (lineIndex >= lines.Length)
            {
                if (pendingChoices != null && pendingChoices.Length > 0)
                {
                    PresentChoices();
                }
                else
                {
                    Close();
                }
            }
            else
            {
                RenderCurrentLine();
            }
        }

        private void PresentChoices()
        {
            awaitingChoice = true;

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < pendingChoices.Length; i++)
            {
                sb.Append(i + 1).Append(". ").Append(pendingChoices[i].Label);
                if (i < pendingChoices.Length - 1) { sb.Append('\n'); }
            }

            if (bodyText != null) { bodyText.text = sb.ToString(); }
            if (continueHintText != null) { continueHintText.text = "Choose 1-" + pendingChoices.Length; }
        }

        private void RenderCurrentLine()
        {
            if (bodyText != null) { bodyText.text = lines[lineIndex]; }

            if (continueHintText != null)
            {
                bool isLast = lineIndex >= lines.Length - 1;
                bool endsInChoices = isLast && pendingChoices != null && pendingChoices.Length > 0;
                continueHintText.text = (isLast && !endsInChoices) ? "F - Close" : "F - Continue";
            }
        }

        public void Close()
        {
            if (panelRoot != null) { panelRoot.SetActive(false); }
            lines = null;
            pendingChoices = null;
            awaitingChoice = false;
            Action callback = onClosed;
            onClosed = null;
            callback?.Invoke();
        }
    }
}
