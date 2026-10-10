using System.Collections.Generic;
using NuclearOption.MissionEditorScripts.Buttons;
using NuclearOption.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PlayableVehicles
{
    internal static class VehicleSelectionMenu
    {
        private const float PadProbeHeight = 4f;
        private const float PadProbeDepth = 30f;
        private const float RefreshInterval = 0.5f;
        private const float TabFontSize = 15f;

        private sealed class Entry
        {
            public VehicleDefinition Definition;
            public float Price;
            public GameObject Root;
            public Image Icon;
            public Image Border;
            public Image Background;
            public Text Label;
            public Text PriceText;
            public RawImage Thumbnail;
            public ShowHoverText Hover;
        }

        private static readonly Vector2 CardSize = new Vector2(124f, 92f);
        private static readonly Color CardBackground = new Color(0.14f, 0.15f, 0.16f, 0.88f);
        private const float CardSpacing = 5f;
        private const int MaxRows = 3;
        private const float ScrollbarWidth = 16f;
        private static RectTransform _gridViewport;
        private static LayoutElement _gridViewportLayout;

        private static GameObject _root;
        private static readonly List<Entry> _entries = new List<Entry>();
        private static readonly List<(Button button, int type)> _tabs = new List<(Button, int)>();
        private static HoverText _hover;

        private static TMP_Text _playerName, _rank, _funds, _depotName, _vehicleName;
        private static GameObject _insufficientRank, _insufficientFunds;
        private static TMP_Text _escalationText;
        private static Image _escalationPointer, _escalationUnderline, _factionLogo;
        private static Transform _conventionalStart, _tacticalStart, _strategicStart, _strategicEnd;
        private static TMP_Text _factionLabel, _factionScore, _factionFunds, _warheads;
        private static GameObject _weaponImageArea, _weaponInfoArea;
        private static Image _weaponImage;
        private static TMP_Text _seeker, _range, _ap, _he, _rcs, _cost, _description;
        private static TMP_Text _stats;
        private static TMP_Text _priceValue, _typeLabel, _typeValue, _remainingValue, _infoLabel1, _infoValue1, _infoLabel2, _infoValue2;
        private static GameObject _unaffordable;
        private static Button _deploy;
        private static TMP_Text _deployText;
        private static GameObject _confirm;
        private static TMP_Text _confirmTitle, _confirmValues;
        private static Light _light;
        private static RectTransform _grid;
        private static readonly List<RectTransform> _layoutChain = new List<RectTransform>();

        private static VehicleDepot _depot;
        private static Entry _selected;
        private static int _tab = -1;
        private static GameObject _preview;
        private static Transform _followProxy;
        private static float _nextRefresh;

        public static bool IsOpen => _root != null && _root.activeSelf;

        public static bool Available => VehicleShop.Enabled && GameAccess.SelectionCameraAvailable;

        public static void Open(VehicleDepot depot)
        {
            if (!GameManager.GetLocalPlayer(out Player player) || !VehicleShop.IsOwnDepot(depot, player.HQ)) return;
            if (_root == null && !Build())
            {
                PurchasePanel.Open(depot);
                return;
            }

            _depot = depot;
            if (GameManager.GetLocalAircraft(out Aircraft aircraft) && aircraft != null && !aircraft.disabled) AircraftAutopilot.Engage(aircraft);
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            _confirm.SetActive(false);
            CursorManager.SetFlag(CursorFlags.SelectionMenu, true);
            GameplayUI.AllowPauseKeybind = false;
            DynamicMap.AllowedToOpen = false;
            GameplayUI ui = SceneSingleton<GameplayUI>.i;
            if (ui != null) ui.HideSelectAirbase();

            SelectTab(_tab);
            Entry first = _selected != null && _entries.Contains(_selected) ? _selected : FirstVisible();
            Select(first, true);
            RebuildLayout();
            Refresh();
        }

        public static void Close()
        {
            Close(true);
        }

        private static void Close(bool returnToMap)
        {
            if (!IsOpen) return;
            _root.SetActive(false);
            _confirm.SetActive(false);
            DestroyPreview();
            _restorePauseKeyFrame = Time.frameCount + 1;
            DynamicMap.AllowedToOpen = true;

            CameraStateManager cam = SceneSingleton<CameraStateManager>.i;
            if (cam != null)
            {
                Transform spawn = _depot != null ? GameAccess.VehicleDepotSpawnTransform(_depot) : null;
                if (spawn != null) cam.transform.position = spawn.position + Vector3.up * 2f;
                cam.SwitchState(cam.freeState);
            }
            if (!VanillaWindow.AnyOpen && Object.FindObjectOfType<AircraftSelectionMenu>() == null) CursorManager.SetFlag(CursorFlags.SelectionMenu, false);
            if (returnToMap) ReturnToPreviousView();
            _depot = null;
        }

        public static void ReturnToPreviousView()
        {
            if (VehicleSession.IsActive || IsOpen) return;
            if (GameManager.GetLocalAircraft(out Aircraft aircraft) && aircraft != null && !aircraft.disabled)
            {
                AircraftAutopilot.Disengage();
                CameraStateManager cam = SceneSingleton<CameraStateManager>.i;
                if (cam != null) cam.SetFollowingUnit(aircraft);
            }
            else
            {
                DynamicMap map = SceneSingleton<DynamicMap>.i;
                if (map != null) map.Maximize();
            }
        }

        public static void Reset()
        {
            if (_root != null)
            {
                GameplayUI.AllowPauseKeybind = true;
                DynamicMap.AllowedToOpen = true;
            }
            _root = null;
            _depot = null;
            _selected = null;
            _entries.Clear();
            _layoutChain.Clear();
            _preview = null;
            _followProxy = null;
            _nextRefresh = 0f;
        }

        private static int _restorePauseKeyFrame = -1;

        public static void Tick()
        {
            if (_restorePauseKeyFrame >= 0 && Time.frameCount >= _restorePauseKeyFrame && !IsOpen)
            {
                _restorePauseKeyFrame = -1;
                GameplayUI.AllowPauseKeybind = true;
            }
            if (!IsOpen) return;
            if (!GameManager.GetLocalPlayer(out Player player) || !VehicleShop.IsOwnDepot(_depot, player.HQ))
            {
                Close();
                return;
            }
            if (GameManager.playerInput != null && GameManager.playerInput.GetButton("Pause") || Input.GetKeyDown(KeyCode.Escape))
            {
                if (_confirm.activeSelf) _confirm.SetActive(false);
                else Close();
                return;
            }
            if (Time.unscaledTime >= _nextRefresh) Refresh();
            UpdateEscalation();
            if (_light != null && _preview != null)
            {
                _light.enabled = NetworkSceneSingleton<LevelInfo>.i != null
                    && (NetworkSceneSingleton<LevelInfo>.i.timeOfDay < 6f || NetworkSceneSingleton<LevelInfo>.i.timeOfDay > 18f);
            }
        }

        private static void Refresh()
        {
            _nextRefresh = Time.unscaledTime + RefreshInterval;
            if (!GameManager.GetLocalPlayer(out Player player) || player.HQ == null) return;
            FactionHQ hq = player.HQ;

            _playerName.text = player.GetDisplayName(PlayerNameContext.Other);
            _rank.text = string.Format("Rank {0}", player.PlayerRank);
            _funds.text = UnitConverter.ValueReading(player.Allocation);
            _depotName.text = DepotName(_depot);

            _factionLabel.text = hq.faction.factionExtendedName;
            _factionScore.text = hq.factionScore.ToString("F1");
            _factionFunds.text = UnitConverter.ValueReading(hq.factionFunds);
            _warheads.text = hq.GetWarheadStockpile().ToString();
            if (_factionLogo != null) _factionLogo.sprite = hq.faction.factionColorLogo;

            foreach (Entry entry in _entries)
            {
                bool available = Availability(player, entry, out _) == null;
                Color color = available ? Color.white : Color.grey;
                bool active = entry == _selected;
                entry.Icon.color = color;
                if (entry.Label != null) entry.Label.color = color;
                if (entry.PriceText != null) entry.PriceText.color = color;
                if (entry.Thumbnail != null)
                {
                    if (entry.Thumbnail.texture == null)
                    {
                        Texture2D texture = VehicleThumbnails.Get(entry.Definition);
                        if (texture != null)
                        {
                            entry.Thumbnail.texture = texture;
                            entry.Thumbnail.enabled = true;
                            entry.Icon.enabled = false;
                        }
                    }
                    entry.Thumbnail.color = color;
                }
                if (entry.Border != null)
                {
                    entry.Border.enabled = active;
                    entry.Border.color = color;
                }
                if (entry.Background != null) entry.Background.color = active ? Color.black : CardBackground;
            }

            UpdateDetails(player);
        }

        private static string Availability(Player player, Entry entry, out bool restricted)
        {
            restricted = true;
            if (player.HQ.preventDonation) return "Purchasing vehicles is disabled in this mission";
            VehicleShop.Result restriction = VehicleShop.Restriction(player, entry.Definition);
            if (restriction != VehicleShop.Result.Purchased) return VehicleShop.RestrictionText(restriction, entry.Definition);
            float cooldown = PurchaseClient.CooldownRemaining(entry.Definition);
            if (cooldown > 0f) return string.Format("Unavailable for {0:F0}s", Mathf.Ceil(cooldown));
            restricted = false;
            if (player.Allocation < entry.Price) return "Insufficient funds";
            return null;
        }

        private static void UpdateDetails(Player player)
        {
            if (_selected == null)
            {
                _vehicleName.text = "";
                _description.text = "Nothing Selected";
                _deploy.interactable = false;
                return;
            }

            VehicleDefinition definition = _selected.Definition;
            string reason = Availability(player, _selected, out bool restricted);
            _vehicleName.text = definition.unitName;
            string staticNote = VehicleDriver.IsStaticDefinition(definition)
                ? "<color=#C8C8C8>Static emplacement: turret control only" + (definition.CanSlingLoad ? ", transported by helicopter sling" : "") + "</color>\n"
                : "";
            _description.text = (reason != null ? "<color=#FF8080>" + reason + "</color>\n" : "") + staticNote + definition.description;

            _priceValue.text = UnitConverter.ValueReading(_selected.Price);
            _typeValue.text = TypeName(definition.vehicleType) + (VehicleDriver.IsStaticDefinition(definition) ? " (STATIC)" : "");
            _remainingValue.text = UnitConverter.ValueReading(player.Allocation - _selected.Price);
            _infoValue1.text = UnitConverter.DistanceReading(MaxWeaponRange(definition));
            _infoValue2.text = string.Format("{0:F0}s", VehicleShop.Cooldown(definition));

            bool unaffordable = player.Allocation < _selected.Price;
            if (_unaffordable != null) _unaffordable.SetActive(unaffordable);
            if (_insufficientFunds != null) _insufficientFunds.SetActive(unaffordable);
            if (_insufficientRank != null) _insufficientRank.SetActive(VehicleShop.Restriction(player, definition) == VehicleShop.Result.Rank);

            _deploy.interactable = reason == null && !PurchaseClient.Pending;
            _deployText.text = reason == null ? "DEPLOY >" : (restricted ? "UNAVAILABLE" : "INSUFFICIENT FUNDS");
        }

        private static void UpdateEscalation()
        {
            MissionManager mission = NetworkSceneSingleton<MissionManager>.i;
            if (mission == null || _escalationPointer == null) return;
            float current = mission.currentEscalation;
            Color baseColor = Color.grey, tactical = Color.yellow, strategic = Color.red;
            Vector3 position;
            Color color;
            string text;
            if (current < mission.tacticalThreshold)
            {
                float t = Mathf.InverseLerp(0f, mission.tacticalThreshold, current);
                position = Vector3.Lerp(_conventionalStart.position, _tacticalStart.position, t);
                color = Color.Lerp(baseColor, tactical, t);
                text = "NON-NUCLEAR";
            }
            else if (current < mission.strategicThreshold)
            {
                float t = Mathf.InverseLerp(mission.tacticalThreshold, mission.strategicThreshold, current);
                position = Vector3.Lerp(_tacticalStart.position, _strategicStart.position, t);
                color = Color.Lerp(tactical, strategic, t);
                text = "TACTICAL NUCLEAR";
            }
            else
            {
                float t = Mathf.InverseLerp(mission.strategicThreshold, 2f * mission.strategicThreshold, current);
                position = Vector3.Lerp(_strategicStart.position, _strategicEnd.position, t);
                color = strategic;
                text = "STRATEGIC NUCLEAR";
            }
            _escalationPointer.transform.position = position;
            _escalationPointer.color = color;
            if (_escalationText != null)
            {
                _escalationText.color = color;
                _escalationText.text = text;
            }
            if (_escalationUnderline != null) _escalationUnderline.color = color;
        }

        private static void ShowWeapon(VehicleDefinition definition)
        {
            WeaponInfo info = MainWeapon(definition);
            bool show = info != null && info.weaponIcon != null;
            _weaponImageArea.SetActive(show);
            _weaponInfoArea.SetActive(show);
            if (!show) return;

            _weaponImage.sprite = info.weaponIcon;
            Missile missile = info.weaponPrefab != null ? info.weaponPrefab.GetComponent<Missile>() : null;
            MissileSeeker seeker = info.weaponPrefab != null ? info.weaponPrefab.GetComponent<MissileSeeker>() : null;
            _seeker.text = seeker != null ? seeker.GetSeekerType() : (info.energy ? "Laser" : "Gun");
            _range.text = "R: " + UnitConverter.DistanceReading(info.targetRequirements.maxRange);
            _ap.text = missile != null ? string.Format("AP: {0}", missile.GetPierce()) : string.Format("AP: {0}", info.pierceDamage);
            _he.text = "HE: " + UnitConverter.YieldReading(missile != null ? missile.GetYield() : info.blastDamage);
            _rcs.text = missile != null ? string.Format("RCS: {0}", missile.definition.radarSize) : "-";
            _cost.text = missile != null ? "C: " + UnitConverter.ValueReading(info.costPerRound) : "-";
        }

        private static void ShowStats(VehicleDefinition definition)
        {
            if (_stats == null) return;
            var sb = new System.Text.StringBuilder();
            GroundVehicle vehicle = definition.unitPrefab != null ? definition.unitPrefab.GetComponent<GroundVehicle>() : null;
            bool isStatic = VehicleDriver.IsStaticDefinition(definition);
            if (isStatic) StatLine(sb, "Mobility", "Static");
            else if (vehicle != null && GameAccess.GroundVehicleTopSpeedOnroad != null && GameAccess.GroundVehicleTopSpeedOffroad != null)
            {
                StatLine(sb, "Road", UnitConverter.SpeedReadingGround(GameAccess.GroundVehicleTopSpeedOnroad(vehicle) / 3.6f));
                StatLine(sb, "Off-road", UnitConverter.SpeedReadingGround(GameAccess.GroundVehicleTopSpeedOffroad(vehicle) / 3.6f));
            }
            StatLine(sb, "Mass", UnitConverter.WeightReading(definition.mass));
            StatLine(sb, "Armor", definition.armorTier.ToString("F0", System.Globalization.CultureInfo.InvariantCulture));

            var features = new List<string>();
            if (vehicle != null && VehicleDeploy.CanDeploy(vehicle)) features.Add(isStatic ? "Self-deploying" : "Deployable");
            if (definition.CanSlingLoad) features.Add("Sling load");
            if (features.Count > 0) sb.Append(string.Join(", ", features)).Append('\n');

            var weapons = new List<string>();
            var ammo = new Dictionary<string, int>();
            if (definition.unitPrefab != null)
            {
                foreach (Weapon weapon in definition.unitPrefab.GetComponentsInChildren<Weapon>(true))
                {
                    WeaponInfo info = weapon != null ? weapon.info : null;
                    if (info == null || info.cargo || info.troops || info.sling || string.IsNullOrEmpty(info.weaponName)) continue;
                    string name = !string.IsNullOrEmpty(info.shortName) ? info.shortName : info.weaponName;
                    if (!ammo.ContainsKey(name))
                    {
                        weapons.Add(name);
                        ammo[name] = 0;
                    }
                    ammo[name] += weapon is Gun gun ? gun.GetFullAmmo() : weapon.ammo;
                }
            }
            foreach (string weapon in weapons)
            {
                int rounds = ammo[weapon];
                sb.Append(weapon).Append(rounds > 0 ? " x" + rounds : "").Append('\n');
            }
            _stats.text = sb.ToString().TrimEnd('\n');
        }

        private static void StatLine(System.Text.StringBuilder sb, string label, string value)
        {
            sb.Append(label).Append(" :<pos=50%>").Append(value).Append('\n');
        }

        private static void BuildStats(Transform root)
        {
            Transform panel = root.Find("LowRow/LeftPanel");
            if (panel == null || _typeLabel == null) return;
            var go = new GameObject("VehicleStats", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(panel, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(12f, 52f);
            rect.offsetMax = new Vector2(-10f, -10f);

            _stats = go.AddComponent<TextMeshProUGUI>();
            _stats.font = _typeLabel.font;
            _stats.fontSharedMaterial = _typeLabel.fontSharedMaterial;
            _stats.color = _typeLabel.color;
            _stats.alignment = TextAlignmentOptions.TopLeft;
            _stats.enableAutoSizing = true;
            _stats.fontSizeMax = _typeLabel.fontSize * 0.8f;
            _stats.fontSizeMin = 8f;
            _stats.raycastTarget = false;
            _stats.enableWordWrapping = false;
            _stats.overflowMode = TextOverflowModes.Overflow;
        }

        private static WeaponInfo MainWeapon(VehicleDefinition definition)
        {
            WeaponInfo best = null;
            if (definition.unitPrefab == null) return null;
            foreach (Weapon weapon in definition.unitPrefab.GetComponentsInChildren<Weapon>(true))
            {
                WeaponInfo info = weapon != null ? weapon.info : null;
                if (info == null || info.cargo || info.troops || info.sling) continue;
                if (best == null || info.targetRequirements.maxRange > best.targetRequirements.maxRange) best = info;
            }
            return best;
        }

        private static float MaxWeaponRange(VehicleDefinition definition)
        {
            WeaponInfo info = MainWeapon(definition);
            return info != null ? info.targetRequirements.maxRange : 0f;
        }

        private static void Select(Entry entry, bool force = false)
        {
            if (entry == null || (!force && entry == _selected)) return;
            _selected = entry;
            ShowWeapon(entry.Definition);
            ShowStats(entry.Definition);
            ShowPreview(entry.Definition);
            Refresh();
        }

        private static void SelectTab(int type)
        {
            _tab = type;
            foreach ((Button button, int tabType) in _tabs) PaintTab(button, tabType == _tab);
            foreach (Entry entry in _entries)
            {
                entry.Root.SetActive(_tab < 0 || (int)entry.Definition.vehicleType == _tab);
            }
            if (_selected == null || !_selected.Root.activeSelf) Select(FirstVisible());
            RebuildLayout();
        }

        private static void RebuildLayout()
        {
            if (_grid == null || !_grid.gameObject.activeInHierarchy) return;
            RebuildChain();
            if (_gridViewport != null && _gridViewportLayout != null)
            {
                GridLayoutGroup layout = _grid.GetComponent<GridLayoutGroup>();
                float padding = layout != null ? layout.padding.top + layout.padding.bottom : 0f;
                float max = padding + MaxRows * CardSize.y + (MaxRows - 1) * CardSpacing;
                float height = Mathf.Min(LayoutUtility.GetPreferredHeight(_grid), max);
                _gridViewportLayout.minHeight = height;
                _gridViewportLayout.preferredHeight = height;
                _gridViewport.sizeDelta = new Vector2(_gridViewport.sizeDelta.x, height);
                _grid.anchoredPosition = Vector2.zero;
                RebuildChain();
            }
        }

        private static void RebuildChain()
        {
            foreach (RectTransform rect in _layoutChain)
            {
                if (rect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }
        }

        private static Entry FirstVisible()
        {
            foreach (Entry entry in _entries)
            {
                if (entry.Root.activeSelf) return entry;
            }
            return _entries.Count > 0 ? _entries[0] : null;
        }

        private static void CycleDepot(int step)
        {
            if (!GameManager.GetLocalHQ(out FactionHQ hq)) return;
            var depots = new List<VehicleDepot>();
            foreach (VehicleDepot depot in Object.FindObjectsOfType<VehicleDepot>())
            {
                if (VehicleShop.IsOwnDepot(depot, hq)) depots.Add(depot);
            }
            if (depots.Count == 0) return;
            depots.Sort((a, b) => string.CompareOrdinal(a.UniqueName, b.UniqueName));
            int index = depots.IndexOf(_depot);
            _depot = depots[((index < 0 ? 0 : index + step) % depots.Count + depots.Count) % depots.Count];
            if (_selected != null) ShowPreview(_selected.Definition);
            Refresh();
        }

        private static void Deploy()
        {
            if (_selected == null || !GameManager.GetLocalPlayer(out Player player)) return;
            _confirmTitle.text = "Deploy " + _selected.Definition.unitName + " ?";
            _confirmValues.text = UnitConverter.ValueReading(player.Allocation) + " \n-" + UnitConverter.ValueReading(_selected.Price)
                + " \n\n" + UnitConverter.ValueReading(player.Allocation - _selected.Price);
            _confirm.SetActive(true);
            _confirm.transform.SetAsLastSibling();
        }

        private static void ConfirmDeploy()
        {
            _confirm.SetActive(false);
            if (_selected == null || _depot == null) return;
            VehicleDepot depot = _depot;
            VehicleDefinition definition = _selected.Definition;
            Close(false);
            PurchaseClient.Request(depot, definition, true);
        }

        private static void ShowPreview(VehicleDefinition definition)
        {
            DestroyPreview();
            if (_depot == null) return;

            Transform depot = _depot.transform;
            Vector3 point = depot.position;
            if (Physics.Raycast(point + Vector3.up * PadProbeHeight, Vector3.down, out RaycastHit hit, PadProbeDepth,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
            }
            Vector3 forward = Vector3.ProjectOnPlane(depot.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            _preview = VehiclePreview.Create(definition, point, Quaternion.LookRotation(forward, Vector3.up));
            if (_preview == null) return;

            Focus(point, VehiclePreview.ViewDistance(definition));
            if (_light != null)
            {
                _light.transform.position = point + Vector3.up * 20f + Vector3.forward * 10f;
                _light.transform.LookAt(point);
            }
        }

        private static void Focus(Vector3 point, float viewDistance)
        {
            CameraStateManager cam = SceneSingleton<CameraStateManager>.i;
            if (cam == null) return;
            CameraSelectionState state = cam.selectionState;
            cam.SetFollowingUnit(null);
            cam.SwitchState(state);

            Vector3 center = point + Vector3.up * 1.75f;
            Transform proxy = GameAccess.SelectionCameraFollowProxy(state);
            if (proxy != null) Object.Destroy(proxy.gameObject);
            _followProxy = new GameObject("CameraFollowProxy").transform;
            _followProxy.SetPositionAndRotation(center, Quaternion.identity);

            GameAccess.SelectionCameraFixedTarget(state) = false;
            GameAccess.SelectionCameraAllowMoveToDropFocus(state) = false;
            GameAccess.SelectionCameraTarget(state) = _preview.transform;
            GameAccess.SelectionCameraFollowProxy(state) = _followProxy;
            GameAccess.SelectionCameraDistance(state) = viewDistance;
            GameAccess.SelectionCameraViewDistance(state) = viewDistance;
            GameAccess.SelectionCameraOrbitalAngle(state) = 30f;

            cam.cameraPivot.SetPositionAndRotation(center, Quaternion.identity);
            cam.transform.SetPositionAndRotation(center + Vector3.forward * (viewDistance + 8f), Quaternion.LookRotation(Vector3.back, Vector3.up));
        }

        private static void DestroyPreview()
        {
            if (_preview != null) Object.Destroy(_preview);
            _preview = null;
        }

        private static bool Build()
        {
            GameplayUI ui = SceneSingleton<GameplayUI>.i;
            GameObject prefab = ui != null ? GameAccess.GameplayUIAircraftSelectionMenu(ui) : null;
            if (prefab == null || ui.gameplayCanvas == null) return false;

            bool wasActive = prefab.activeSelf;
            GameObject root;
            prefab.SetActive(false);
            try
            {
                root = Object.Instantiate(prefab, ui.gameplayCanvas.transform, false);
            }
            finally
            {
                prefab.SetActive(wasActive);
            }
            root.name = "PlayableVehicles_VehicleSelectionMenu";

            try
            {
                if (!Wire(root.transform))
                {
                    Object.Destroy(root);
                    ModLog.Warning("Vehicle selection menu: vanilla menu layout changed, using the simple purchase panel");
                    return false;
                }
            }
            catch (System.Exception e)
            {
                Object.Destroy(root);
                ModLog.Warning("Vehicle selection menu failed to build: " + e);
                return false;
            }
            _root = root;
            return true;
        }

        private static bool Wire(Transform t)
        {
            var inventory = t.GetComponentInChildren<AircraftInventoryMenu>(true);
            GameObject buttonPrefab = inventory != null ? inventory.buttonPrefab : null;
            if (buttonPrefab == null) return false;

            foreach (MonoBehaviour behaviour in t.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour is AircraftSelectionMenu || behaviour is AircraftInventoryMenu || behaviour is LoadoutSelector || behaviour is ContributeToFaction)
                {
                    Object.DestroyImmediate(behaviour);
                }
            }
            foreach (string path in new[] { "ReserveNotice", "ReserveConfirmation", "ReturnConfirmation", "ContributePanel", "noHangarsPanel",
                "LowRow/LeftPanel/LiverySelector", "LowRow/BottomPanel/LoadoutSelector", "LowRow/RightPanel/InfoPanel/Container/FuelSelector",
                "TopPanel/TopMiddlePanel/AirbaseInfoRow/AirbaseWarheads", "TopPanel/TopRightPanel/FactionStatsPanel/ContributeButton" })
            {
                Transform part = t.Find(path);
                if (part != null) part.gameObject.SetActive(false);
            }

            string top = "TopPanel/TopMiddlePanel/";
            _playerName = Text(t, top + "PlayerInfoRow/PilotName/PlayerName");
            _rank = Text(t, top + "PlayerInfoRow/PilotRank/RankDisplay");
            _funds = Text(t, top + "PlayerInfoRow/PilotFunds/AllocationDisplay");
            _insufficientRank = t.Find(top + "PlayerInfoRow/PilotRank/InsufficientRank")?.gameObject;
            _insufficientFunds = t.Find(top + "PlayerInfoRow/PilotFunds/InsufficientFunds")?.gameObject;
            _depotName = Text(t, top + "AirbaseInfoRow/AirbaseName");
            _vehicleName = Text(t, top + "AircraftInfoRow/AircraftName");
            SetButton(t, top + "AirbaseInfoRow/previousAirbase", () => CycleDepot(-1));
            SetButton(t, top + "AirbaseInfoRow/nextAirbase", () => CycleDepot(1));

            string escalation = "TopPanel/TopRightPanel/EscalationPanel/";
            _conventionalStart = t.Find(escalation + "Thresholds/conventionalStart");
            _tacticalStart = t.Find(escalation + "Thresholds/tacticalStart");
            _strategicStart = t.Find(escalation + "Thresholds/strategicStart");
            _strategicEnd = t.Find(escalation + "Thresholds/strategicEnd");
            _escalationPointer = t.Find(escalation + "Thresholds/escalationPointer")?.GetComponent<Image>();
            _escalationText = Text(t, escalation + "Thresholds/escalationPointer/Text (TMP)");
            _escalationUnderline = t.Find(escalation + "Thresholds/escalationPointer/Text (TMP)/underline")?.GetComponent<Image>();
            _factionLogo = t.Find(escalation + "FactionLogo")?.GetComponent<Image>();
            string stats = "TopPanel/TopRightPanel/FactionStatsPanel/";
            _factionLabel = Text(t, stats + "FactionLabel");
            _factionScore = Text(t, stats + "FactionScoreRow/FactionScore");
            _factionFunds = Text(t, stats + "FactionFundsRow/FactionFunds");
            _warheads = Text(t, stats + "WarheadsRow/Warheads");

            string bottom = "LowRow/BottomPanel/Center/Darkener/";
            _weaponImageArea = t.Find(bottom + "WeaponImage")?.gameObject;
            _weaponImage = _weaponImageArea != null ? _weaponImageArea.GetComponent<Image>() : null;
            _weaponInfoArea = t.Find(bottom + "WeaponInfo")?.gameObject;
            _seeker = Text(t, bottom + "WeaponInfo/weaponRange/seeker_value");
            _range = Text(t, bottom + "WeaponInfo/weaponRange/range_value");
            _ap = Text(t, bottom + "WeaponInfo/weaponWarhead/ap_value");
            _he = Text(t, bottom + "WeaponInfo/weaponWarhead/he_value");
            _rcs = Text(t, bottom + "WeaponInfo/weaponCost/rcs_value");
            _cost = Text(t, bottom + "WeaponInfo/weaponCost/cost_value");
            _description = Text(t, bottom + "Description");
            SetButton(t, "LowRow/LeftPanel/ReturnButton", Close);

            string info = "LowRow/RightPanel/InfoPanel/Container/";
            SetText(t, info + "LoadoutValue", "Price :\n\n");
            _priceValue = Text(t, info + "LoadoutValue/LoadoutValue");
            _unaffordable = t.Find(info + "LoadoutValue/LoadoutUnaffordable")?.gameObject;
            _typeLabel = SetText(t, info + "LoadoutWeight", "Type :\n\n");
            _typeValue = Text(t, info + "LoadoutWeight/LoadoutWeight");
            SetText(t, info + "TotalWeight", "Remaining :\n\n");
            _remainingValue = Text(t, info + "TotalWeight/GrossWeight");
            Transform overWeight = t.Find(info + "TotalWeight/OverWeight");
            if (overWeight != null) overWeight.gameObject.SetActive(false);
            _infoLabel1 = SetText(t, info + "Info/TWRLabel", "Range :\n\n");
            _infoValue1 = Text(t, info + "Info/TWRLabel/TWR");
            _infoLabel2 = SetText(t, info + "Info/RCSLabel", "Cooldown :\n\n");
            _infoValue2 = Text(t, info + "Info/RCSLabel/RCS");
            _deploy = SetButton(t, "LowRow/RightPanel/InfoPanel/FlyButton", Deploy);
            BuildStats(t);
            _deployText = _deploy != null ? _deploy.GetComponentInChildren<TMP_Text>(true) : null;

            _confirm = t.Find("BuyConfirmation")?.gameObject;
            _confirmTitle = Text(t, "BuyConfirmation/BuyText");
            _confirmValues = Text(t, "BuyConfirmation/BuyText (1)");
            SetButton(t, "BuyConfirmation/OKButton", ConfirmDeploy);
            SetButton(t, "BuyConfirmation/CancelButton", () => _confirm.SetActive(false));

            _light = t.Find("SelectionLight")?.GetComponent<Light>();

            _hover = t.Find("SelectionHoverText")?.GetComponent<HoverText>();
            foreach (ShowHoverText show in t.GetComponentsInChildren<ShowHoverText>(true))
            {
                if (_hover != null) show.SetHover(_hover);
                else Object.DestroyImmediate(show);
            }

            if (_playerName == null || _rank == null || _funds == null || _depotName == null || _vehicleName == null
                || _factionLabel == null || _factionScore == null || _factionFunds == null || _warheads == null
                || _weaponImageArea == null || _weaponImage == null || _weaponInfoArea == null || _seeker == null || _range == null
                || _ap == null || _he == null || _rcs == null || _cost == null || _description == null
                || _priceValue == null || _typeValue == null || _remainingValue == null || _infoValue1 == null || _infoValue2 == null
                || _deploy == null || _deployText == null || _confirm == null || _confirmTitle == null || _confirmValues == null
                || _conventionalStart == null || _tacticalStart == null || _strategicStart == null || _strategicEnd == null)
            {
                return false;
            }
            _description.richText = true;

            return BuildTabs(t, top) && BuildGrid(t, top, buttonPrefab);
        }

        private static bool BuildTabs(Transform t, string top)
        {
            Transform row = t.Find(top + "InventoryMenu/ActionsButtonsRow");
            Transform template = row != null ? row.Find("BUYButton") : null;
            if (template == null) return false;
            for (int i = 0; i < row.childCount; i++) row.GetChild(i).gameObject.SetActive(false);
            Transform expand = t.Find(top + "InventoryMenu/ExpandButton");
            if (expand != null) expand.gameObject.SetActive(false);

            var types = new SortedSet<int>();
            foreach (VehicleDefinition definition in Catalog()) types.Add((int)definition.vehicleType);

            _tabs.Clear();
            AddTab(template, row, "ALL", -1);
            foreach (int type in types) AddTab(template, row, TypeName((VehicleType)type).ToUpperInvariant(), type);
            return true;
        }

        private static void AddTab(Transform template, Transform row, string text, int type)
        {
            GameObject go = Object.Instantiate(template.gameObject, row, false);
            go.SetActive(true);
            go.name = "Tab_" + text;
            TMP_Text label = go.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = text;
                label.enableAutoSizing = true;
                label.fontSizeMin = 10f;
                label.fontSizeMax = TabFontSize;
                var labelRect = label.rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.sizeDelta = new Vector2(-4f, 0f);
            }
            Button button = go.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => SelectTab(type));
            go.GetComponent<ShowHoverText>()?.SetText(text);
            _tabs.Add((button, type));
            PaintTab(button, type == _tab);
        }

        private static void PaintTab(Button button, bool selected)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = selected ? new Color32(0x40, 0x80, 0x40, 0xFF) : new Color32(0x65, 0x69, 0x6E, 0xCC);
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
        }

        private static bool BuildGrid(Transform t, string top, GameObject buttonPrefab)
        {
            Transform grid = t.Find(top + "InventoryMenu/AircraftButtons");
            if (grid == null) return false;
            grid.gameObject.SetActive(true);

            _grid = (RectTransform)grid;
            _grid.pivot = new Vector2(_grid.pivot.x, 1f);
            WrapGridInScroll(t, grid);
            _layoutChain.Clear();
            for (Transform part = grid; part != null && part != t; part = part.parent)
            {
                if (part is RectTransform rect) _layoutChain.Add(rect);
            }

            _entries.Clear();
            var definitions = Catalog();
            foreach (VehicleDefinition definition in definitions) VehicleThumbnails.Get(definition);
            definitions.Sort((a, b) => VehicleShop.Price(a).CompareTo(VehicleShop.Price(b)));
            foreach (VehicleDefinition definition in definitions)
            {
                Entry entry = CreateButton(buttonPrefab, grid, definition);
                if (entry != null) _entries.Add(entry);
            }
            return _entries.Count > 0;
        }

        private static void WrapGridInScroll(Transform root, Transform grid)
        {
            GridLayoutGroup layout = grid.GetComponent<GridLayoutGroup>();
            if (layout != null)
            {
                layout.cellSize = CardSize;
                layout.spacing = new Vector2(CardSpacing, CardSpacing);
                layout.childAlignment = TextAnchor.UpperCenter;
            }
            ContentSizeFitter fitter = grid.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            Transform parent = grid.parent;
            int index = grid.GetSiblingIndex();
            var viewportGo = new GameObject("GridViewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image), typeof(LayoutElement));
            var viewport = (RectTransform)viewportGo.transform;
            viewport.SetParent(parent, false);
            viewport.SetSiblingIndex(index);
            viewport.anchorMin = new Vector2(0f, 1f);
            viewport.anchorMax = new Vector2(1f, 1f);
            viewport.pivot = new Vector2(0.5f, 1f);
            viewport.sizeDelta = new Vector2(0f, CardSize.y);
            viewportGo.GetComponent<Image>().color = Color.clear;
            _gridViewport = viewport;
            _gridViewportLayout = viewportGo.GetComponent<LayoutElement>();

            grid.SetParent(viewport, false);
            _grid.anchorMin = new Vector2(0f, 1f);
            _grid.anchorMax = new Vector2(1f, 1f);
            _grid.pivot = new Vector2(0.5f, 1f);
            _grid.anchoredPosition = Vector2.zero;
            _grid.sizeDelta = new Vector2(-ScrollbarWidth, 0f);

            var scroll = viewportGo.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = _grid;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = CardSize.y * 0.5f;

            Transform source = root.Find("LowRow/LeftPanel/LiverySelector/LiveryDropdown/Template/Scrollbar");
            if (source != null)
            {
                GameObject barGo = Object.Instantiate(source.gameObject, viewport, false);
                barGo.SetActive(true);
                var bar = (RectTransform)barGo.transform;
                bar.anchorMin = new Vector2(1f, 0f);
                bar.anchorMax = new Vector2(1f, 1f);
                bar.pivot = new Vector2(1f, 0.5f);
                bar.offsetMin = new Vector2(-ScrollbarWidth, 0f);
                bar.offsetMax = Vector2.zero;
                scroll.verticalScrollbar = barGo.GetComponent<Scrollbar>();
                scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            }
        }

        private static Entry CreateButton(GameObject prefab, Transform parent, VehicleDefinition definition)
        {
            GameObject go = Object.Instantiate(prefab, parent, false);
            Object.DestroyImmediate(go.GetComponent<AircraftSelectionButton>());
            go.name = "VehicleButton_" + definition.unitName;

            var entry = new Entry
            {
                Definition = definition,
                Price = VehicleShop.Price(definition),
                Root = go,
                Icon = go.transform.Find("Icon")?.GetComponent<Image>(),
                Border = go.transform.Find("Square")?.GetComponent<Image>(),
                Background = go.GetComponent<Image>(),
                Label = go.transform.Find("Label")?.GetComponent<Text>(),
                PriceText = go.transform.Find("NumberOwned")?.GetComponent<Text>(),
                Hover = go.GetComponent<ShowHoverText>(),
            };
            if (entry.Icon == null) return null;
            LayoutCard(go, entry, definition);
            if (entry.Hover != null)
            {
                if (_hover != null)
                {
                    entry.Hover.SetHover(_hover);
                    entry.Hover.SetText(definition.unitName + "\n" + UnitConverter.ValueReading(entry.Price));
                }
                else Object.DestroyImmediate(entry.Hover);
            }

            Button button = go.GetComponent<Button>();
            if (button != null)
            {
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(() => Select(entry));
                button.interactable = true;
                Behaviour animator = go.GetComponent("Animator") as Behaviour;
                if (animator != null) animator.enabled = false;
                if (entry.Background != null) entry.Background.material = null;
                button.transition = Selectable.Transition.ColorTint;
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.selectedColor = Color.white;
                colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
                colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                colors.colorMultiplier = 1f;
                button.colors = colors;
            }
            return entry;
        }

        private static void LayoutCard(GameObject go, Entry entry, VehicleDefinition definition)
        {
            if (entry.Border != null) Stretch(entry.Border.rectTransform, Vector2.zero, Vector2.zero);

            entry.Icon.sprite = definition.mapIcon != null ? definition.mapIcon : definition.friendlyIcon;
            entry.Icon.rectTransform.anchoredPosition = new Vector2(0f, 8f);

            var thumbGo = new GameObject("Thumbnail", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            var thumbRect = (RectTransform)thumbGo.transform;
            thumbRect.SetParent(go.transform, false);
            thumbRect.SetSiblingIndex(entry.Icon.transform.GetSiblingIndex());
            Stretch(thumbRect, new Vector2(3f, 18f), new Vector2(-3f, -3f));
            entry.Thumbnail = thumbGo.GetComponent<RawImage>();
            entry.Thumbnail.raycastTarget = false;
            entry.Thumbnail.enabled = false;

            if (entry.Label != null)
            {
                RectTransform label = entry.Label.rectTransform;
                label.anchorMin = new Vector2(0f, 0f);
                label.anchorMax = new Vector2(1f, 0f);
                label.pivot = new Vector2(0.5f, 0f);
                label.anchoredPosition = new Vector2(0f, 1f);
                label.sizeDelta = new Vector2(-6f, 17f);
                entry.Label.text = definition.unitName;
                entry.Label.alignment = TextAnchor.MiddleCenter;
                entry.Label.resizeTextForBestFit = true;
                entry.Label.resizeTextMinSize = 8;
                entry.Label.resizeTextMaxSize = 13;
                entry.Label.horizontalOverflow = HorizontalWrapMode.Wrap;
                entry.Label.verticalOverflow = VerticalWrapMode.Truncate;
                AddShadow(entry.Label);
            }
            if (entry.PriceText != null)
            {
                RectTransform price = entry.PriceText.rectTransform;
                price.anchorMin = price.anchorMax = new Vector2(0f, 1f);
                price.pivot = new Vector2(0f, 1f);
                price.anchoredPosition = new Vector2(5f, -3f);
                price.sizeDelta = new Vector2(CardSize.x - 10f, 14f);
                entry.PriceText.text = UnitConverter.ValueReading(VehicleShop.Price(definition));
                entry.PriceText.alignment = TextAnchor.UpperLeft;
                entry.PriceText.fontSize = 12;
                entry.PriceText.resizeTextForBestFit = false;
                entry.PriceText.enabled = true;
                entry.PriceText.gameObject.SetActive(true);
                AddShadow(entry.PriceText);
            }
            Transform supply = go.transform.Find("NumberSupply");
            if (supply != null) supply.gameObject.SetActive(false);
        }

        private static void AddShadow(Graphic text)
        {
            if (text.GetComponent<Shadow>() != null) return;
            Shadow shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static List<VehicleDefinition> Catalog()
        {
            var list = new List<VehicleDefinition>();
            List<VehicleDefinition> catalog = VehicleShop.Catalog;
            for (int i = 0; catalog != null && i < catalog.Count; i++)
            {
                if (VehicleShop.TryGet(i, out VehicleDefinition definition)) list.Add(definition);
            }
            return list;
        }

        private static string TypeName(VehicleType type)
        {
            List<Encyclopedia.UnitType> types = Encyclopedia.i != null ? Encyclopedia.i.vehicleTypes : null;
            int index = (int)type;
            if (types != null && types.Count == System.Enum.GetValues(typeof(VehicleType)).Length && types[index] != null
                && !string.IsNullOrEmpty(types[index].typeName))
            {
                return types[index].typeName;
            }
            switch (type)
            {
                case VehicleType.LCV: return "Light Vehicle";
                case VehicleType.ART: return "Artillery";
                case VehicleType.IR_SAM: return "IR SAM";
                case VehicleType.R_SAM: return "Radar SAM";
                case VehicleType.RDR: return "Radar";
                case VehicleType.TRUCK: return "Truck";
                default: return type.ToString();
            }
        }

        private static string DepotName(VehicleDepot depot)
        {
            return depot != null ? depot.unitName.ToUpperInvariant() : "";
        }

        private static TMP_Text Text(Transform root, string path)
        {
            Transform t = root.Find(path);
            return t != null ? t.GetComponent<TMP_Text>() : null;
        }

        private static TMP_Text SetText(Transform root, string path, string text)
        {
            TMP_Text label = Text(root, path);
            if (label != null) label.text = text;
            return label;
        }

        private static Button SetButton(Transform root, string path, UnityAction onClick)
        {
            Transform t = root.Find(path);
            Button button = t != null ? t.GetComponent<Button>() : null;
            if (button == null) return null;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(onClick);
            button.interactable = true;
            return button;
        }
    }
}
