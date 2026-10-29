using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlayableVehicles
{
    internal static class FireControlPanel
    {
        // Height grew by 40px to make room for the barrel-elevation slider row under the
        // scrollable target list (ListBottom moved from 222 to 182). The slider occupies the
        // band between the vanilla button bar (top edge at ~50px) and the status line:
        //   track centered at SliderY = 72 (row height clamped to 16 -> spans 64..80),
        //   caption baseline at SliderY + SliderCaptionOffset = 92 (text box spans ~86..98).
        // Nothing above 98 reaches into the list viewport starting at ListBottom = 182, and
        // nothing below 64 touches the buttons. AddLine rows stay at their original heights.
        private static readonly Vector2 Size = new Vector2(512f, 480f);
        private const float ListTop = 56f;
        private const float ListBottom = 182f;
        private const float SliderY = 72f;
        private const float SliderCaptionOffset = 20f;
        private const string AutoCaption = "Barrel elevation: AUTO";
        private const string ManualCaptionPrefix = "Barrel elevation: ";
        private const float HighBranchDegrees = 45f;
        private const float RefreshInterval = 0.5f;

        private static VanillaWindow _window;
        private static VanillaWindow.Row _lowRow;
        private static VanillaWindow.Row _highRow;
        private static VanillaWindow.Row _autoRow;
        private static TMP_Text _target;
        private static TMP_Text _range;
        private static TMP_Text _line3;
        private static TMP_Text _line3Label;
        private static TMP_Text _line4;
        private static TMP_Text _line4Label;
        private static TMP_Text _status;

        private static FireMissionKind _kind;
        private static FireMode _artilleryMode = FireMode.High;
        private static Unit _shownTarget;
        private static ArtillerySolver.Solution _low;
        private static ArtillerySolver.Solution _high;
        private static float _nextRefresh;

        // Elevation slider: replaces the binary Low/High choice with a continuous barrel angle.
        // The locked target stays where it was assigned; only the trajectory branch changes as
        // the player drags. Null until the window is built; hidden for non-artillery stations.
        private static Slider _elevationSlider;
        private static TMP_Text _elevationCaption;
        private static bool _suppressSlider;   // ignore programmatic SetValueWithoutNotify echoes
        private static float _sliderValue = float.NaN;
        private static ArtillerySolver.Solution _manual;

        public static bool IsOpen => _window != null && _window.IsOpen;

        private static FireMode Mode => _kind == FireMissionKind.Artillery ? _artilleryMode : FireMode.Missile;

        public static void Toggle()
        {
            if (IsOpen)
            {
                Close();
                return;
            }

            VehicleDriver driver = VehicleSession.IsActive ? VehicleSession.Driver : null;
            if (driver == null || VehicleSession.Vehicle == null) return;
            FireMissionKind kind = driver.Turrets.MissionKind;
            if (kind == FireMissionKind.None)
            {
                ModLog.Notify("Fire control: select an artillery or missile weapon");
                return;
            }
            if (!TurretController.DeployedForMission(VehicleSession.Vehicle))
            {
                ModLog.Notify("Fire control: deploy the vehicle first [" + ModConfig.DeployKey.Value + "]");
                return;
            }
            if ((_window == null || !_window.Alive) && !Build())
            {
                ModLog.Notify("Fire control is not available: game menu not found");
                return;
            }

            SetKind(kind);
            if (driver.Turrets.MissionActive && kind == FireMissionKind.Artillery && driver.Turrets.MissionMode != FireMode.Missile)
            {
                _artilleryMode = driver.Turrets.MissionMode;
            }
            if (kind == FireMissionKind.Artillery && driver.Turrets.HasManualElevation)
            {
                // An active manual override: the slider shows the fixed angle and the preview
                // comes from sampling at exactly that elevation.
                _manual = driver.Turrets.MissionActive && driver.Turrets.MissionTarget != null
                    ? driver.Turrets.PreviewElevation(driver.Turrets.MissionTarget, driver.Turrets.MissionElevation)
                    : default;
                _sliderValue = driver.Turrets.MissionElevation;
                _artilleryMode = driver.Turrets.MissionElevation <= 45f ? FireMode.Low : FireMode.High;
            }
            else
            {
                _sliderValue = float.NaN;
                _manual = default;
            }
            if (kind == FireMissionKind.Artillery) SyncSlider(driver.Turrets, true);
            _window.Show();
            FireControlMap.Open(_window.Rect);
            Tick();
        }

        public static void Close()
        {
            _window?.Hide();
            FireControlMap.Close();
        }

        public static void Reset()
        {
            _window = null;
            _lowRow = null;
            _highRow = null;
            _autoRow = null;
            _target = null;
            _range = null;
            _line3 = null;
            _line3Label = null;
            _line4 = null;
            _line4Label = null;
            _status = null;
            _elevationSlider = null;
            _elevationCaption = null;
            _suppressSlider = false;
            _sliderValue = float.NaN;
            _manual = default;
            _kind = FireMissionKind.None;
            _shownTarget = null;
            _low = default;
            _high = default;
            _nextRefresh = 0f;
            FireControlMap.Reset();
        }

        public static void Tick()
        {
            if (!IsOpen) return;

            VehicleDriver driver = VehicleSession.IsActive ? VehicleSession.Driver : null;
            GroundVehicle vehicle = VehicleSession.Vehicle;
            FireMissionKind kind = driver != null ? driver.Turrets.MissionKind : FireMissionKind.None;
            if (driver == null || vehicle == null || vehicle.disabled || kind == FireMissionKind.None || !TurretController.DeployedForMission(vehicle))
            {
                Close();
                return;
            }
            TurretController turrets = driver.Turrets;
            if (kind != _kind) SetKind(kind);
            FireControlMap.Tick(_window.Rect);

            Unit target = FindTarget(turrets, vehicle);
            if (target != _shownTarget || Time.unscaledTime >= _nextRefresh)
            {
                _shownTarget = target;
                _nextRefresh = Time.unscaledTime + RefreshInterval;
                if (_kind == FireMissionKind.Artillery)
                {
                    _low = target != null ? turrets.PreviewMission(target, FireMode.Low) : default;
                    _high = target != null ? turrets.PreviewMission(target, FireMode.High) : default;
                    // Keep the slider preview fresh while dragging (moving targets, recoil).
                    if (turrets.HasManualElevation && target != null)
                    {
                        _manual = turrets.PreviewElevation(target, turrets.MissionElevation);
                        _artilleryMode = turrets.MissionElevation <= 45f ? FireMode.Low : FireMode.High;
                    }
                }
                else
                {
                    _low = target != null ? turrets.PreviewMission(target, FireMode.Missile) : default;
                }
                _lowRow.State = _highRow.State = -1;
            }

            ArtillerySolver.Solution solution = CurrentSolution();
            bool ready = target != null && solution.Status == ArtillerySolver.Status.Ready;
            WeaponStation station = turrets.SelectedStation;

            if (_kind == FireMissionKind.Artillery)
            {
                UpdateRow(_lowRow, "DIRECT FIRE (LOW ANGLE)", FireMode.Low, target, _low);
                UpdateRow(_highRow, "INDIRECT FIRE (HIGH ANGLE)", FireMode.High, target, _high);
                // Mark which branch is driving the panel: the slider angle when a manual
                // override is armed, otherwise the AUTO row.
                bool manual = turrets.HasManualElevation;
                if (_autoRow != null && _autoRow.State != (manual ? 0 : 1))
                {
                    _autoRow.State = manual ? 0 : 1;
                    _autoRow.Label.text = manual ? "   AUTO ELEVATION" : "> AUTO ELEVATION";
                }
                _line3.text = ready ? solution.Elevation.ToString("F1") + "°" : "-";
                _line4.text = ready ? solution.TimeOfFlight.ToString("F0") + " s" : "-";
            }
            else
            {
                UpdateRow(_lowRow, WeaponCaption(station), FireMode.Missile, target, _low);
                _line3.text = _kind == FireMissionKind.Ballistic ? WarheadText(station)
                    : target != null ? UnitConverter.AltitudeReading(target.radarAlt) : "-";
                _line4.text = station != null ? AmmoSync.Of(VehicleSession.Vehicle, station).Ammo + " / " + station.FullAmmo : "-";
            }

            _target.text = target != null ? target.unitName : "-";
            _range.text = target != null ? UnitConverter.DistanceReading(solution.Range) : "-";
            _status.text = StatusText(turrets, target, solution);

            _window.Confirm.interactable = ready;
            _window.SetButtonText(_window.Cancel, turrets.MissionActive ? "CEASE FIRE" : "CANCEL");
        }

        private static ArtillerySolver.Solution CurrentSolution()
        {
            if (_kind == FireMissionKind.Artillery)
            {
                TurretController turrets = VehicleSession.IsActive ? VehicleSession.Driver?.Turrets : null;
                if (turrets != null && turrets.HasManualElevation) return _manual;
                return _artilleryMode == FireMode.High ? _high : _low;
            }
            return _low;
        }

        // Mirror the turret controller's manual-elevation state into the slider. force=true also
        // re-clamps the value when the bounds changed (different weapon station selected).
        private static void SyncSlider(TurretController turrets, bool force)
        {
            if (_elevationSlider == null || turrets == null) return;
            Turret artillery = turrets.ArtilleryTurret(turrets.SelectedStation);
            TurretController.MissionElevationBounds(artillery, out float min, out float max);
            _suppressSlider = true;
            if (force || _elevationSlider.minValue != min || _elevationSlider.maxValue != max)
            {
                _elevationSlider.minValue = min;
                _elevationSlider.maxValue = max;
            }
            float wanted = turrets.HasManualElevation ? Mathf.Clamp(turrets.MissionElevation, min, max) : DefaultElevation(min, max);
            if (force || !Mathf.Approximately(_sliderValue, wanted))
            {
                _sliderValue = wanted;
                _elevationSlider.SetValueWithoutNotify(wanted);
            }
            if (_elevationCaption != null)
            {
                _elevationCaption.text = turrets.HasManualElevation
                    ? ManualCaptionPrefix + wanted.ToString("F1") + "\u00b0"
                    : AutoCaption;
            }
            _suppressSlider = false;
        }

        private static float DefaultElevation(float min, float max)
        {
            // Open the slider at the branch matching the currently selected Low/High preset so
            // dragging it from there feels continuous.
            float lowish = Mathf.Max(min, 5f);
            return _artilleryMode == FireMode.High ? Mathf.Min(70f, max) : lowish;
        }

        private static void OnSliderChanged(float degrees)
        {
            if (_suppressSlider) return;
            _sliderValue = degrees;
            VehicleDriver driver = VehicleSession.IsActive ? VehicleSession.Driver : null;
            if (driver == null || _kind != FireMissionKind.Artillery) return;
            driver.Turrets.SetMissionElevation(degrees);
            _artilleryMode = degrees <= 45f ? FireMode.Low : FireMode.High;
            _manual = _shownTarget != null ? driver.Turrets.PreviewElevation(_shownTarget, degrees) : default;
            _line3Label.text = "Elevation:";
            Tick();
        }

        private static void SetKind(FireMissionKind kind)
        {
            _kind = kind;
            _shownTarget = null;
            _nextRefresh = 0f;
            _lowRow.State = _highRow.State = -1;
            bool artillery = kind == FireMissionKind.Artillery;
            _highRow.Root.SetActive(artillery);
            if (_elevationSlider != null) _elevationSlider.gameObject.SetActive(artillery);
            if (_elevationCaption != null) _elevationCaption.gameObject.SetActive(artillery);
            _line3Label.text = artillery ? "Elevation:" : kind == FireMissionKind.Ballistic ? "Warhead:" : "Target altitude:";
            _line4Label.text = artillery ? "Time of flight:" : "Missiles:";
        }

        private static void UpdateRow(VanillaWindow.Row row, string caption, FireMode mode, Unit target, in ArtillerySolver.Solution solution)
        {
            bool available = target != null && solution.Status == ArtillerySolver.Status.Ready;
            int state = (available ? 1 : 0) | (Mode == mode ? 2 : 0) | ((int)solution.Status << 2);
            if (state == row.State) return;
            row.State = state;

            row.Label.text = (Mode == mode ? "> " : "   ") + caption;
            string reason = target == null || available ? null : " ( " + Reason(solution.Status) + " )";
            VanillaWindow.SetRowState(row, target == null || available, reason);
            row.Hover?.SetText(mode == FireMode.High ? "Lobbed shell over terrain and cover. Longer time of flight"
                : mode == FireMode.Low ? "Flat trajectory, shortest time of flight"
                : _kind == FireMissionKind.Ballistic ? "The missile flies its own ballistic trajectory to the target's known position"
                : "The missile guides itself to the assigned air target");
        }

        private static string WeaponCaption(WeaponStation station)
        {
            WeaponInfo info = station != null ? station.WeaponInfo : null;
            return info != null && !string.IsNullOrEmpty(info.weaponName) ? info.weaponName.ToUpperInvariant() : "MISSILE";
        }

        private static string WarheadText(WeaponStation station)
        {
            WeaponInfo info = station != null ? station.WeaponInfo : null;
            if (info != null && info.nuclear) return "Nuclear";
            MissileLauncher launcher = FireMissions.Launcher(station);
            return launcher != null && launcher.missile != null ? launcher.missile.unitName : "-";
        }

        private static string Reason(ArtillerySolver.Status status)
        {
            switch (status)
            {
                case ArtillerySolver.Status.ElevationLimit: return "Beyond elevation limits";
                case ArtillerySolver.Status.TooClose: return "Too close";
                case ArtillerySolver.Status.NotAirborne: return "Not an air target";
                case ArtillerySolver.Status.AltitudeLimit: return "Target too high";
                case ArtillerySolver.Status.NoRadar: return "No radar link";
                case ArtillerySolver.Status.NoLineOfSight: return "No line of sight";
                default: return "Out of range";
            }
        }

        private static string StatusText(TurretController turrets, Unit target, in ArtillerySolver.Solution solution)
        {
            bool artillery = _kind == FireMissionKind.Artillery;
            if (turrets.MissionActive && turrets.MissionTarget == target && turrets.MissionMode == Mode)
            {
                return artillery ? "ENGAGING - FIRE WHEN READY" : "TARGET ASSIGNED - FIRE TO LAUNCH";
            }
            if (target == null) return _kind == FireMissionKind.AirDefense ? "SELECT AN AIR TARGET ON THE MAP" : "SELECT A TARGET ON THE MAP";
            if (solution.Status == ArtillerySolver.Status.Ready) return artillery ? "SOLUTION READY" : "READY TO LAUNCH";
            if (solution.Status == ArtillerySolver.Status.NoRadar) return "NO RADAR LINK - NEED RADAR WITHIN 300 M";
            return Reason(solution.Status).ToUpperInvariant();
        }

        private static void Select(FireMode mode)
        {
            if (_kind != FireMissionKind.Artillery) return;
            _artilleryMode = mode;
            // Choosing a preset hands control back to the automatic solver: the slider moves to
            // the angle of that branch and returns to AUTO (unless the mission is already being
            // engaged at a fixed angle, in which case it snaps to the matching preset angle).
            VehicleDriver driver = VehicleSession.IsActive ? VehicleSession.Driver : null;
            TurretController turrets = driver != null ? driver.Turrets : null;
            if (turrets != null)
            {
                float solutionAngle = mode == FireMode.High ? _high.Elevation : _low.Elevation;
                bool valid = float.IsFinite(solutionAngle) && solutionAngle > -90f && solutionAngle < 90f;
                if (turrets.HasManualElevation)
                {
                    if (valid) turrets.SetMissionElevation(solutionAngle);
                    else turrets.ClearMissionElevation();
                }
                else if (valid)
                {
                    // Preview only: snap the slider handle to the branch angle without arming
                    // the manual override, so AUTO keeps tracking the moving target.
                    TurretController.MissionElevationBounds(turrets.ArtilleryTurret(turrets.SelectedStation), out float min, out float max);
                    _sliderValue = Mathf.Clamp(solutionAngle, min, max);
                    _elevationSlider?.SetValueWithoutNotify(_sliderValue);
                }
            }
            _manual = default;
            _lowRow.State = _highRow.State = -1;
            if (turrets != null) SyncSlider(turrets, true);
            Tick();
        }

        // "AUTO ELEVATION" row: drop the fixed-angle override so the solver picks the Low/High
        // branch again and the slider returns to its AUTO caption.
        private static void SelectAuto()
        {
            VehicleDriver driver = VehicleSession.IsActive ? VehicleSession.Driver : null;
            TurretController turrets = driver != null ? driver.Turrets : null;
            if (turrets == null || _kind != FireMissionKind.Artillery) return;
            turrets.ClearMissionElevation();
            _manual = default;
            _sliderValue = float.NaN;
            _lowRow.State = _highRow.State = -1;
            SyncSlider(turrets, true);
            Tick();
        }

        private static void Engage()
        {
            VehicleDriver driver = VehicleSession.Driver;
            if (driver == null || _shownTarget == null) return;
            ArtillerySolver.Solution solution = CurrentSolution();
            if (solution.Status != ArtillerySolver.Status.Ready) return;

            // SetMission alone only points the turrets at the target; missile seekers are
            // handed their target via Weapon.SetTarget in FireSelectedWeapon, which reads
            // _lock.Target. Without a lock the SAM flies with an unprogrammed seeker and
            // self-destructs almost immediately after launch. Establish the same lock the
            // mouse-target-select path creates before arming the fire mission.
            driver.Turrets.SetLock(_shownTarget);
            driver.Turrets.SetMission(_shownTarget, Mode);
            string text = "Fire mission: " + _shownTarget.unitName + ", " + UnitConverter.DistanceReading(solution.Range);
            text += _kind == FireMissionKind.Artillery
                ? ", time of flight " + solution.TimeOfFlight.ToString("F0") + " s. Fire when ready"
                : ". Fire to launch";
            ModLog.Notify(text);
            Close();
        }

        private static void CancelOrCeaseFire()
        {
            VehicleDriver driver = VehicleSession.Driver;
            if (driver != null && driver.Turrets.MissionActive) driver.Turrets.ClearMission("Cease fire");
            Close();
        }

        private static Unit FindTarget(TurretController turrets, GroundVehicle vehicle)
        {
            FactionHQ hq = vehicle.NetworkHQ;
            if (IsTarget(turrets.LockedTarget, hq)) return turrets.LockedTarget;

            DynamicMap map = SceneSingleton<DynamicMap>.i;
            if (map != null && map.selectedIcons != null)
            {
                for (int i = map.selectedIcons.Count - 1; i >= 0; i--)
                {
                    if (map.selectedIcons[i] is UnitMapIcon icon && IsTarget(icon.unit, hq)) return icon.unit;
                }
            }

            CombatHUD combatHud = SceneSingleton<CombatHUD>.i;
            List<Unit> targets = combatHud != null ? combatHud.GetTargetList() : null;
            if (targets != null)
            {
                for (int i = targets.Count - 1; i >= 0; i--)
                {
                    if (IsTarget(targets[i], hq)) return targets[i];
                }
            }
            return turrets.MissionActive && IsTarget(turrets.MissionTarget, hq) ? turrets.MissionTarget : null;
        }

        private static bool IsTarget(Unit unit, FactionHQ hq)
        {
            if (unit == null || unit.disabled || (hq != null && unit.NetworkHQ == hq)) return false;
            bool air = unit is Aircraft || unit is Missile;
            return _kind == FireMissionKind.AirDefense ? air : !air;
        }

        private static bool Build()
        {
            _window = VanillaWindow.Create("FireControlPanel", "FIRE CONTROL", Size, new Vector2(1f, 0.5f), new Vector2(-40f, 0f),
                ListTop, ListBottom, false);
            if (_window == null) return false;

            _target = _window.AddLine("Target:", 192f);
            _range = _window.AddLine("Range:", 164f);
            _line3 = _window.AddLine("Elevation:", 136f, out _line3Label);
            _line4 = _window.AddLine("Time of flight:", 108f, out _line4Label);
            _status = _window.AddLine("", 80f);
            _status.rectTransform.sizeDelta = new Vector2(440f, _status.rectTransform.sizeDelta.y);
            _status.enableWordWrapping = false;
            _status.fontSizeMax = _status.fontSize;
            _status.fontSizeMin = _status.fontSize * 0.6f;
            _status.enableAutoSizing = true;

            _lowRow = _window.AddRow("DIRECT FIRE (LOW ANGLE)", null, () => Select(FireMode.Low));
            _highRow = _window.AddRow("INDIRECT FIRE (HIGH ANGLE)", null, () => Select(FireMode.High));
            // Third row inside the list: hands control back to the automatic Low/High solver.
            VanillaWindow.Row autoRow = _window.AddRow("AUTO ELEVATION", null, SelectAuto);
            if (_lowRow == null || _highRow == null)
            {
                _window.Destroy();
                _window = null;
                return false;
            }
            _autoRow = autoRow;

            if (_autoRow != null)
            {
                // The AUTO row has no ballistic solution to gate it on; keep it clickable and
                // give it a hover hint. Its "> " marker is refreshed by Tick().
                VanillaWindow.SetRowState(_autoRow, true, null);
                _autoRow.Hover?.SetText("Release the fixed barrel angle and let the solver pick the Low/High branch automatically");
                _autoRow.Label.text = "   AUTO ELEVATION";
            }

            _window.SetButtonText(_window.Confirm, "ENGAGE");
            _window.Confirm.onClick.AddListener(Engage);
            _window.Cancel.onClick.AddListener(CancelOrCeaseFire);

            // Continuous barrel-angle slider: instead of only Low/High presets the player can
            // fix any elevation between the turret's travel limits; the target stays locked and
            // every solution is sampled at exactly that angle. Sits in the band under the status
            // line (track at SliderY = 72, caption at 92) and above the vanilla button bar; the
            // scroll list viewport starts at ListBottom = 182 so nothing collides.
            _elevationSlider = _window.AddSlider(AutoCaption, SliderY, SliderCaptionOffset, 300f,
                TurretController.MinMissionElevation, TurretController.MaxMissionElevation,
                TurretController.ElevationStep, false, OnSliderChanged);
            if (_elevationSlider != null)
            {
                // The caption is created as a sibling of the slider row inside the window
                // content and named "SliderCaption" (see VanillaWindow.AddSlider).
                Transform caption = _window.Content != null ? _window.Content.Find("SliderCaption") : null;
                _elevationCaption = caption != null ? caption.GetComponent<TMP_Text>() : null;
                _elevationSlider.gameObject.SetActive(_kind == FireMissionKind.Artillery);
                if (_elevationCaption != null) _elevationCaption.gameObject.SetActive(_kind == FireMissionKind.Artillery);
            }
            return true;
        }
    }
}
