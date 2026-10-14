using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace VRRocket
{
    /// <summary>Which words the menu shows: the first play, or the menu that returns after a successful flight.</summary>
    public enum MenuState { FirstPlay, AfterSuccess }

    /// <summary>Every word the menu shows in one state, edited in the Inspector on the menu's <see cref="MenuPanel"/>.</summary>
    [Serializable]
    public sealed class MenuWords
    {
        public string title = "VR ROCKET";
        [Tooltip("The small line under the title")] public string kicker = "ASSEMBLE  ·  LAUNCH  ·  BE MISSION CONTROL";
        [TextArea(2, 6)] public string body = "Everyone wants to be an astronaut,\nBut only cool people build rockets.\nWanna be cool?";
        [Tooltip("The left button: plays the briefing (the room's intro), then building starts")] public string briefButton = "BRIEF ME";
        [Tooltip("The right button: straight to building, no briefing")] public string buildButton = "BUILD ROCKET";
        public string footer = "UCT VR 2026  ·  HAND-ASSEMBLED MODEL ROCKET";
    }

    /// <summary>
    /// The world-space start menu: title, kicker, body copy, two buttons (BRIEF ME plays the intro, BUILD ROCKET goes straight to
    /// building) and a footer. Every word, for each state, is edited here in the Inspector (<see cref="firstPlay"/>,
    /// <see cref="afterSuccess"/>); the canvas texts are written from them, and in the Editor the canvas previews the state picked
    /// in Preview. Fades in and out through a CanvasGroup. The buttons' onClick are wired to
    /// <see cref="GameFlowController.StartGame"/> and <see cref="GameFlowController.StartWithoutIntro"/> in the scene.
    /// </summary>
    public sealed class MenuPanel : MonoBehaviour
    {
        [SerializeField] CanvasGroup m_Group;
        [Header("Words")]
        [SerializeField, Tooltip("The menu at the start of a play")] MenuWords m_FirstPlay = new MenuWords();
        [SerializeField, Tooltip("The menu that comes back after a successful flight")] MenuWords m_AfterSuccess = new MenuWords
        {
            title = "MISSION SUCCESS",
            kicker = "FLIGHT COMPLETE  ·  ALL STATIONS NOMINAL",
            body = "Your rocket flew clean.\nThink you can do it again?",
            briefButton = "BRIEF ME",
            buildButton = "BUILD AGAIN",
        };
        [SerializeField, Tooltip("The left button's words in a room with no briefing to play: it then starts building, and the right button is hidden")]
        string m_NoBriefingButton = "START";
        [SerializeField, Tooltip("Editor only: which state the canvas shows while you edit")] MenuState m_Preview = MenuState.FirstPlay;
        [Header("Canvas")]
        [SerializeField] TextMeshProUGUI m_Title;
        [SerializeField] TextMeshProUGUI m_Kicker;
        [SerializeField] TextMeshProUGUI m_Body;
        [SerializeField, Tooltip("The left (briefing) button's label")] TextMeshProUGUI m_ButtonLabel;
        [SerializeField, Tooltip("The right (build) button")] GameObject m_BuildButton;
        [SerializeField] TextMeshProUGUI m_BuildButtonLabel;
        [SerializeField] TextMeshProUGUI m_Footer;
        [SerializeField] float m_FadeSeconds = 0.35f;

        Coroutine m_Fade;

        public bool isShown { get; private set; }
        /// <summary>The state last shown.</summary>
        public MenuState shownState { get; private set; }
        public MenuWords firstPlay => m_FirstPlay;
        public MenuWords afterSuccess => m_AfterSuccess;
        public string noBriefingButton { get => m_NoBriefingButton; set => m_NoBriefingButton = value; }
        public MenuWords Words(MenuState state) => state == MenuState.AfterSuccess ? m_AfterSuccess : m_FirstPlay;

        public void Configure(CanvasGroup group, TextMeshProUGUI title, TextMeshProUGUI body, TextMeshProUGUI buttonLabel)
        {
            m_Group = group; m_Title = title; m_Body = body; m_ButtonLabel = buttonLabel;
        }

        public void ConfigureMore(TextMeshProUGUI kicker, GameObject buildButton, TextMeshProUGUI buildButtonLabel, TextMeshProUGUI footer)
        {
            m_Kicker = kicker; m_BuildButton = buildButton; m_BuildButtonLabel = buildButtonLabel; m_Footer = footer;
        }

        /// <summary>Shows the menu with the words of <paramref name="state"/>. Without a briefing to play the left button starts building and the right one is hidden.</summary>
        public void Show(MenuState state, bool briefingAvailable)
        {
            shownState = state;
            Apply(Words(state), briefingAvailable);
            gameObject.SetActive(true);
            isShown = true;
            Fade(1f);
        }

        public void Hide()
        {
            isShown = false;
            Fade(0f);
        }

        void Apply(MenuWords words, bool briefingAvailable)
        {
            Set(m_Title, words.title);
            Set(m_Kicker, words.kicker);
            Set(m_Body, words.body);
            Set(m_ButtonLabel, briefingAvailable ? words.briefButton : m_NoBriefingButton);
            Set(m_BuildButtonLabel, words.buildButton);
            Set(m_Footer, words.footer);
            if (m_BuildButton != null && m_BuildButton.activeSelf != briefingAvailable) m_BuildButton.SetActive(briefingAvailable);
        }

        static void Set(TMP_Text text, string value)
        {
            if (text == null) return;
            value = value ?? "";
            if (text.text == value) return;
            text.text = value;
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(text);
#endif
        }

#if UNITY_EDITOR
        // Edits in the Inspector show on the canvas straight away (the state picked in Preview), so what you see is what plays.
        void OnValidate()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorApplication.delayCall += () => { if (this != null && !Application.isPlaying) Apply(Words(m_Preview), true); };
        }
#endif

        void Fade(float target)
        {
            if (m_Group == null) { if (target <= 0f) gameObject.SetActive(false); return; }
            if (!isActiveAndEnabled) { m_Group.alpha = target; m_Group.interactable = target > 0f; m_Group.blocksRaycasts = target > 0f; if (target <= 0f) gameObject.SetActive(false); return; }
            if (m_Fade != null) StopCoroutine(m_Fade);
            m_Fade = StartCoroutine(FadeRoutine(target));
        }

        IEnumerator FadeRoutine(float target)
        {
            var start = m_Group.alpha;
            var elapsed = 0f;
            m_Group.interactable = target > 0f;
            m_Group.blocksRaycasts = target > 0f;
            while (elapsed < m_FadeSeconds)
            {
                elapsed += Time.deltaTime;
                m_Group.alpha = Mathf.Lerp(start, target, elapsed / m_FadeSeconds);
                yield return null;
            }
            m_Group.alpha = target;
            if (target <= 0f) gameObject.SetActive(false);
            m_Fade = null;
        }
    }
}
