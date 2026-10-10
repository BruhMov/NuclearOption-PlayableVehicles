using UnityEngine;
using UnityEngine.UI;

namespace PlayableVehicles
{
    internal sealed class OpticWidget : HudWidget
    {
        private static readonly Color ReticleColor = new Color(1f, 0.3f, 0.2f, 0.85f);

        private RectTransform _root;

        public override void Build(RectTransform root, HudContext context)
        {
            _root = HudStyle.CreateFill("Optic", root);

            var vignette = HudStyle.CreateFill("Vignette", _root).gameObject.AddComponent<RawImage>();
            vignette.texture = HudStyle.CreateVignetteTexture(256);
            vignette.raycastTarget = false;

            var reticle = HudStyle.CreateRect("Reticle", _root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 120f));
            const float bw = 100f, bh = 60f, len = 18f, t = 2f;

            Line(reticle, new Vector2(-bw + len / 2, bh), new Vector2(len, t));
            Line(reticle, new Vector2(-bw, bh - len / 2), new Vector2(t, len));
            Line(reticle, new Vector2(bw - len / 2, bh), new Vector2(len, t));
            Line(reticle, new Vector2(bw, bh - len / 2), new Vector2(t, len));
            Line(reticle, new Vector2(-bw + len / 2, -bh), new Vector2(len, t));
            Line(reticle, new Vector2(-bw, -bh + len / 2), new Vector2(t, len));
            Line(reticle, new Vector2(bw - len / 2, -bh), new Vector2(len, t));
            Line(reticle, new Vector2(bw, -bh + len / 2), new Vector2(t, len));

            Line(reticle, Vector2.zero, new Vector2(t, t));
            Line(reticle, new Vector2(0f, -18f), new Vector2(48f, t));
            Line(reticle, new Vector2(0f, -34f), new Vector2(32f, t));
            Line(reticle, new Vector2(0f, -50f), new Vector2(16f, t));

            _root.gameObject.SetActive(false);
        }

        private static void Line(RectTransform parent, Vector2 position, Vector2 size)
        {
            HudStyle.CreateImage("Line", parent, new Vector2(0.5f, 0.5f), position, size, ReticleColor);
        }

        public override void Refresh(HudContext context)
        {
            bool zoomed = context.View.IsZoomed;
            if (_root.gameObject.activeSelf != zoomed) _root.gameObject.SetActive(zoomed);
        }
    }
}
