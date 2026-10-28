using UnityEngine;

namespace ButterflyEffect.Prototype
{
    public sealed class PrototypeHud : MonoBehaviour
    {
        public DesktopPlayer player;
        private GUIStyle title, body, small, centre, button, panel;
        private Texture2D background;
        private readonly string[] stations = { "1  Cigarette", "2  Campfire", "3  Water bucket", "4  Fireworks" };

        private void PrepareStyles()
        {
            if (body != null) return;
            background = new Texture2D(1, 1); background.SetPixel(0, 0, new Color(0.025f, 0.055f, 0.07f, 0.93f)); background.Apply();
            body = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true, richText = true };
            body.normal.textColor = new Color(0.93f, 0.97f, 0.96f);
            title = new GUIStyle(body) { fontSize = 21, fontStyle = FontStyle.Bold };
            small = new GUIStyle(body) { fontSize = 14 };
            centre = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter, fontSize = 17 };
            button = new GUIStyle(GUI.skin.button) { fontSize = 15, wordWrap = true };
            panel = new GUIStyle(GUI.skin.box); panel.normal.background = background;
        }

        private void OnGUI()
        {
            PrototypeSession session = PrototypeSession.Instance;
            if (session == null || player == null) return;
            PrepareStyles();
            float scale = Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
            Matrix4x4 old = GUI.matrix; GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            float width = Screen.width / scale, height = Screen.height / scale;
            GUI.Box(new Rect(18, 16, width - 36, 62), "", panel);
            GUI.Label(new Rect(32, 22, 480, 30), "THE BUTTERFLY EFFECT", title);
            GUI.Label(new Rect(32, 51, 480, 22), "Interaction prototype / Mahmoud", small);
            GUI.Label(new Rect(width - 500, 32, 465, 34), player.Held != null
                ? "In hand: " + player.Held.itemName + "   |   E to put down"
                : "Hands free   |   Aim at an item and tap E", body);
            GUI.Label(new Rect(width / 2f - 15, height / 2f - 20, 30, 40), "+", centre);
            GUI.Box(new Rect(width / 2f - 410, 88, 820, 52), "", panel);
            GUI.Label(new Rect(width / 2f - 396, 94, 792, 44), session.message, centre);
            string hint = player.Held != null ? player.Held.HeldHint : player.TargetHint;
            if (!string.IsNullOrEmpty(hint))
            {
                GUI.Label(new Rect(width / 2f - 430, height - 140, 860, 34), hint, centre);
            }
            if (player.Held is MarshmallowItem marshmallow)
                Progress(width, 148, Mathf.Clamp01(marshmallow.cookedSeconds / marshmallow.goldenSeconds),
                    marshmallow.IsGolden ? "GOLDEN" : marshmallow.cookedSeconds >= marshmallow.burntSeconds ? "BURNT" : "ROASTING");
            if (player.Held is WaterBucketItem bucket)
                Progress(width, 148, bucket.waterLeft / bucket.capacity, "WATER");
            GUI.Box(new Rect(18, height - 99, width - 36, 81), "", panel);
            GUI.Label(new Rect(30, height - 94, width - 60, 24),
                "WASD move   |   Hold right mouse: look   |   E grab/drop   |   Hold Q, release to throw   |   F use   |   Hold R pour", small);
            for (int i = 0; i < 4; i++)
                if (GUI.Button(new Rect(30 + i * 151, height - 64, 140, 34), stations[i], button)) player.GoToStation(i);
            if (GUI.Button(new Rect(width - 340, height - 64, 145, 34), "Choices / Tab", button)) session.ShowDebrief();
            if (GUI.Button(new Rect(width - 182, height - 64, 150, 34), "Reset / Backspace", button)) session.ResetRun();
            if (session.showDebrief) Debrief(session, width, height);
            GUI.matrix = old;
        }

        private void Progress(float width, float y, float fraction, string text)
        {
            GUI.Box(new Rect(width / 2f - 220, y, 440, 26), "", panel);
            Color before = GUI.color; GUI.color = new Color(0.2f, 0.7f, 0.85f);
            GUI.DrawTexture(new Rect(width / 2f - 216, y + 4, 432 * Mathf.Clamp01(fraction), 18), Texture2D.whiteTexture);
            GUI.color = before; GUI.Label(new Rect(width / 2f - 210, y - 1, 420, 28), text, small);
        }

        private void Debrief(PrototypeSession session, float width, float height)
        {
            Rect area = new Rect(width / 2f - 410, height / 2f - 210, 820, 430);
            GUI.Box(area, "", panel);
            ChoiceState s = session.state;
            GUI.Label(new Rect(area.x + 24, area.y + 20, 770, 44), "What followed you home?", title);
            GUI.Label(new Rect(area.x + 24, area.y + 72, 770, 40), ChoiceState.EndingTitle(s.PreviewEnding(session.endingRule)), title);
            string text = "Cigarette: " + (s.cigarette == CigaretteCondition.Disposed ? "disposed of" : "not disposed of")
                + (s.cigaretteGrassBurning ? ". The dry grass is still hot." : ".")
                + "\nCampfire: " + FireText(s.campfire) + "." 
                + "\nFireworks: " + (s.fireworksEverLaunched ? "launched" : "left unlit")
                + (s.fireworksGrassBurning ? ". Sparks ignited the grass." : ".")
                + "\nMarshmallows eaten: " + s.marshmallowsEaten + "    Fireworks launched: " + s.fireworksLaunched
                + "\n\n" + (session.endingRule == EndingRule.OriginalPitch
                    ? "This preview counts unsafe choices, even after cleanup."
                    : "This preview counts unresolved hazards left behind.")
                + "\nThe forest may feel far away. The places we care about are connected.";
            GUI.Label(new Rect(area.x + 24, area.y + 125, 770, 225), text, body);
            if (GUI.Button(new Rect(area.x + 24, area.y + 365, 245, 42), "Stay a little longer", button)) session.showDebrief = false;
            if (GUI.Button(new Rect(area.x + 287, area.y + 365, 245, 42), "Try another path", button)) session.ResetRun();
        }

        private static string FireText(FireCondition condition)
        {
            return condition == FireCondition.Burning ? "Burning" : condition == FireCondition.HotEmbers ? "Hot embers - keep pouring" : "Cold / extinguished";
        }
        private void OnDestroy() { if (background != null) Destroy(background); }
    }
}
