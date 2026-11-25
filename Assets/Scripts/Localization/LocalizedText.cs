using UnityEngine.UIElements;

namespace Polymulator
{
    [UxmlElement]
    public sealed partial class LocalizedText : Label
    {
        private string locKey = string.Empty;
        private bool subscribed;

        [UxmlAttribute("loc-key")]
        public string LocKey
        {
            get => locKey;
            set
            {
                locKey = value ?? string.Empty;
                RefreshText();
            }
        }

        public LocalizedText()
        {
            RegisterCallback<AttachToPanelEvent>(OnAttachedToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachedFromPanel);
        }

        private void OnAttachedToPanel(AttachToPanelEvent _)
        {
            if (subscribed) return;
            subscribed = true;
            L.OnLanguageChanged += RefreshText;
            RefreshText();
        }

        private void OnDetachedFromPanel(DetachFromPanelEvent _)
        {
            if (!subscribed) return;
            subscribed = false;
            L.OnLanguageChanged -= RefreshText;
        }

        private void RefreshText()
        {
            text = L.T(locKey);
        }
    }
}
