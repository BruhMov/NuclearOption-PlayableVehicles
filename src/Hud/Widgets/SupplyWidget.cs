using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlayableVehicles
{
    internal sealed class SupplyWidget : HudWidget
    {
        private const int MaxRequests = 4;
        private const float ScanInterval = 0.25f;
        private const float Width = 340f;
        private const float FontSize = 18f;
        private const float Padding = 6f;
        private static readonly Vector2 Anchor = new Vector2(1f, 0.5f);
        private static readonly Vector2 Position = new Vector2(-24f, 230f);

        private struct Request
        {
            public Unit Unit;
            public float Distance;
        }

        private Rearmer _rearmer;
        private Image _backing;
        private TextMeshProUGUI _text;
        private float _nextScan;
        private string _shown;
        private readonly List<Request> _requests = new List<Request>();
        private readonly StringBuilder _builder = new StringBuilder();
        private readonly DistanceText _rangeText = new DistanceText("radius ");
        private readonly DistanceText[] _distanceTexts = new DistanceText[MaxRequests];

        public override void Build(RectTransform root, HudContext context)
        {
            _rearmer = context.Vehicle.GetComponent<Rearmer>();
            if (_rearmer == null) return;

            for (int i = 0; i < MaxRequests; i++) _distanceTexts[i] = new DistanceText();
            _backing = HudStyle.CreateImage("Supply", root, Anchor, Position, new Vector2(Width, 30f), HudStyle.Panel);
            RectTransform rect = _backing.rectTransform;
            rect.pivot = new Vector2(1f, 1f);
            _text = HudStyle.CreateText("SupplyText", rect, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, FontSize, TextAlignmentOptions.TopLeft);
            RectTransform textRect = _text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(Padding * 1.5f, Padding);
            textRect.offsetMax = new Vector2(-Padding, -Padding);
            _text.richText = true;
        }

        public override void Refresh(HudContext context)
        {
            if (_rearmer == null || _text == null) return;
            float now = Time.unscaledTime;
            if (now < _nextScan) return;
            _nextScan = now + ScanInterval;

            CollectRequests(context.Vehicle);
            string text = Compose();
            if (text == _shown) return;
            _shown = text;
            _text.text = text;
            _text.color = HudStyle.Normal;
            int lines = 2 + Mathf.Max(_requests.Count, 1);
            _backing.rectTransform.sizeDelta = new Vector2(Width, lines * FontSize * 1.25f + Padding * 2f);
        }

        private void CollectRequests(GroundVehicle vehicle)
        {
            _requests.Clear();
            FactionHQ hq = vehicle.NetworkHQ;
            RearmMissionController controller = hq != null ? hq.RearmMissionController : null;
            if (controller == null || controller.UnitsNeedingRearm == null) return;

            GlobalPosition from = _rearmer.GetPosition();
            foreach (Unit unit in controller.UnitsNeedingRearm)
            {
                if (unit == null || unit.disabled || unit.radarAlt > 10f || unit is Aircraft || unit is Ship || unit == vehicle) continue;
                _requests.Add(new Request { Unit = unit, Distance = FastMath.Distance(unit.GlobalPosition(), from) });
            }
            _requests.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            if (_requests.Count > MaxRequests) _requests.RemoveRange(MaxRequests, _requests.Count - MaxRequests);
        }

        private string Compose()
        {
            _builder.Length = 0;
            float max = _rearmer.GetMaxCapacity();
            int percent = max > 0f ? Mathf.RoundToInt(Mathf.Clamp01(_rearmer.Capacity / max) * 100f) : 0;
            _builder.Append("SUPPLY ").Append(percent).Append("%   ").Append(Dim(_rangeText.Get(_rearmer.Range))).Append('\n');
            if (_requests.Count == 0)
            {
                _builder.Append(Dim("No rearm requests nearby"));
                return _builder.ToString();
            }
            _builder.Append(Dim("Rearm requests (drive within radius):"));
            for (int i = 0; i < _requests.Count; i++)
            {
                Request request = _requests[i];
                string line = "Rearm: " + request.Unit.unitName + "  " + _distanceTexts[i].Get(request.Distance);
                _builder.Append('\n').Append(request.Distance <= _rearmer.Range ? line : Dim(line));
            }
            return _builder.ToString();
        }

        private static string Dim(string text)
        {
            return "<alpha=#80>" + text + "<alpha=#FF>";
        }
    }
}
