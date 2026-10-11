using System.Collections;
using TMPro;
using UnityEngine;

namespace VRRocket
{
    /// <summary>
    /// The world-space start menu: a glass panel with a title, a line of copy and one big button. Fades in and out through a CanvasGroup.
    /// The button's onClick is wired to <see cref="GameFlowController.StartGame"/> in the scene.
    /// </summary>
    public sealed class MenuPanel : MonoBehaviour
    {
        [SerializeField] CanvasGroup m_Group;
        [SerializeField] TextMeshProUGUI m_Title;
        [SerializeField] TextMeshProUGUI m_Body;
        [SerializeField] TextMeshProUGUI m_ButtonLabel;
        [SerializeField] float m_FadeSeconds = 0.35f;

        Coroutine m_Fade;

        public bool isShown { get; private set; }

        public void Configure(CanvasGroup group, TextMeshProUGUI title, TextMeshProUGUI body, TextMeshProUGUI buttonLabel)
        {
            m_Group = group; m_Title = title; m_Body = body; m_ButtonLabel = buttonLabel;
        }

        public void Show(string title, string body, string buttonLabel)
        {
            if (m_Title != null) m_Title.text = title;
            if (m_Body != null) m_Body.text = body;
            if (m_ButtonLabel != null) m_ButtonLabel.text = buttonLabel;
            gameObject.SetActive(true);
            isShown = true;
            Fade(1f);
        }

        public void Hide()
        {
            isShown = false;
            Fade(0f);
        }

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
