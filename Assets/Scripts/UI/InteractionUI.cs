using UnityEngine;

namespace Polymulator
{
    public class InteractionUI : MonoBehaviour
    {
        [Header("References")]
        public ObjectInteractor interactor;
        public ObjectCarryController carryController;

        private Texture2D panelTexture;
        private Texture2D accentTexture;
        private Texture2D warningTexture;
        private Texture2D carryTexture;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;
        private GUIStyle promptStyle;
        private GUIStyle warningStyle;
        private GUIStyle carryStyle;

        private void Awake()
        {
            Color panel = PaletteSettings.Crust;
            panel.a = 0.92f;

            panelTexture = CreateTexture(panel);
            accentTexture = CreateTexture(PaletteSettings.Sapphire);
            warningTexture = CreateTexture(PaletteSettings.Peach);
            carryTexture = CreateTexture(PaletteSettings.Green);
        }

        private void OnGUI()
        {
            if (interactor == null) interactor = Object.FindFirstObjectByType<ObjectInteractor>();
            if (carryController == null) carryController = Object.FindFirstObjectByType<ObjectCarryController>();
            EnsureStyles();

            float margin = 24f;
            float width = Screen.width;
            float height = Screen.height;

            Rect header = new Rect(margin, margin, 340f, 96f);
            DrawPanel(header, accentTexture);
            GUI.Label(new Rect(header.x + 18f, header.y + 12f, 300f, 30f), "POLYMULATOR", titleStyle);
            GUI.Label(new Rect(header.x + 18f, header.y + 42f, 300f, 20f), "SIMULATION // PROTOTYPE 01", subtitleStyle);
            GUI.Label(new Rect(header.x + 18f, header.y + 66f, 300f, 18f), "RECT  //  SPATIAL MANIPULATION", smallStyle);

            Rect controls = new Rect(margin, height - margin - 116f, 310f, 116f);
            DrawPanel(controls, accentTexture);
            GUI.Label(new Rect(controls.x + 18f, controls.y + 14f, 270f, 20f), "CONTROLS", subtitleStyle);
            GUI.Label(new Rect(controls.x + 18f, controls.y + 40f, 270f, 20f), "WASD / ARROWS   MOVE", bodyStyle);
            GUI.Label(new Rect(controls.x + 18f, controls.y + 64f, 270f, 20f), "E   PICK UP / RELEASE", bodyStyle);
            GUI.Label(new Rect(controls.x + 18f, controls.y + 88f, 270f, 20f), "FIXED 2.5D CAMERA", smallStyle);

            if (carryController != null && carryController.IsCarrying)
            {
                Rect carryRect = new Rect(width - margin - 280f, margin, 280f, 52f);
                DrawPanel(carryRect, carryTexture);
                GUI.Label(new Rect(carryRect.x + 18f, carryRect.y + 8f, 245f, 18f), "CARRYING", smallStyle);
                GUI.Label(new Rect(carryRect.x + 18f, carryRect.y + 24f, 245f, 22f),
                    carryController.CarriedObjectName, carryStyle);
            }

            string prompt = null;
            Texture2D promptAccent = accentTexture;
            GUIStyle promptStyleToUse = promptStyle;
            if (carryController != null && carryController.IsCarrying)
            {
                prompt = "[ E ]   RELEASE   " + carryController.CarriedObjectName;
            }
            else if (interactor != null && interactor.HasTarget)
            {
                if (interactor.TargetCanBePickedUp)
                    prompt = "[ E ]   PICK UP   " + interactor.TargetName;
                else
                {
                    prompt = "LOCKED   " + interactor.TargetName + "   //   CANNOT BE PICKED UP";
                    promptAccent = warningTexture;
                    promptStyleToUse = warningStyle;
                }
            }

            if (!string.IsNullOrEmpty(prompt))
            {
                float promptWidth = Mathf.Min(580f, width - 48f);
                Rect promptRect = new Rect((width - promptWidth) * 0.5f, height - 106f, promptWidth, 48f);
                DrawPanel(promptRect, promptAccent);
                GUI.Label(new Rect(promptRect.x + 16f, promptRect.y, promptRect.width - 32f, promptRect.height),
                    prompt, promptStyleToUse);
            }
        }

        private void DrawPanel(Rect rect, Texture2D accent)
        {
            GUI.DrawTexture(rect, panelTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 4f, rect.height), accent);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;

            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 24;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = PaletteSettings.TextPrimary;

            subtitleStyle = new GUIStyle(GUI.skin.label);
            subtitleStyle.fontSize = 12;
            subtitleStyle.fontStyle = FontStyle.Bold;
            subtitleStyle.normal.textColor = PaletteSettings.Sapphire;

            bodyStyle = new GUIStyle(GUI.skin.label);
            bodyStyle.fontSize = 13;
            bodyStyle.normal.textColor = PaletteSettings.TextSecondary;

            smallStyle = new GUIStyle(GUI.skin.label);
            smallStyle.fontSize = 11;
            smallStyle.normal.textColor = PaletteSettings.TextSubtext;

            promptStyle = new GUIStyle(GUI.skin.label);
            promptStyle.fontSize = 17;
            promptStyle.fontStyle = FontStyle.Bold;
            promptStyle.alignment = TextAnchor.MiddleCenter;
            promptStyle.normal.textColor = PaletteSettings.TextPrimary;

            warningStyle = new GUIStyle(promptStyle);
            warningStyle.normal.textColor = PaletteSettings.Peach;

            carryStyle = new GUIStyle(GUI.skin.label);
            carryStyle.fontSize = 16;
            carryStyle.fontStyle = FontStyle.Bold;
            carryStyle.normal.textColor = PaletteSettings.Green;
        }

        private static Texture2D CreateTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }

        private void OnDestroy()
        {
            if (panelTexture != null) Destroy(panelTexture);
            if (accentTexture != null) Destroy(accentTexture);
            if (warningTexture != null) Destroy(warningTexture);
            if (carryTexture != null) Destroy(carryTexture);
        }
    }
}
