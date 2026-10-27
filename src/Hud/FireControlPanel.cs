using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class FireControlPanel
    {
        private static readonly Vector2 Size = new Vector2(512f, 440f);
        private const float ListTop = 56f;
        private const float ListBottom = 222f;
        private const float RefreshInterval = 0.5f;

        private static VanillaWindow _window;
        private static VanillaWindow.Row _lowRow;
        private static VanillaWindow.Row _highRow;
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
            _target = null;
            _range = null;
            _line3 = null;
            _line3Label = null;
            _line4 = null;
            _line4Label = null;
            _status = null;
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
            return _kind == FireMissionKind.Artillery && _artilleryMode == FireMode.High ? _high : _low;
        }

        private static void SetKind(FireMissionKind kind)
        {
            _kind = kind;
            _shownTarget = null;
            _nextRefresh = 0f;
            _lowRow.State = _highRow.State = -1;
            bool artillery = kind == FireMissionKind.Artillery;
            _highRow.Root.SetActive(artillery);
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
            _lowRow.State = _highRow.State = -1;
            Tick();
        }

        private static void Engage()
        {
            VehicleDriver driver = VehicleSession.Driver;
            if (driver == null || _shownTarget == null) return;
            ArtillerySolver.Solution solution = CurrentSolution();
            // Ручное назначение огневой задачи из окна Fire Control: пишем цель, её фактический тип и radarAlt.
            // Когда кнопка неактивна ("Not an air target"), по этой строке видно, какая именно проверка не прошла:
            // type без Aircraft/Missile - цель неверно распознана, маленький radarAlt при большом worldY - высота недостоверна.
            ModLog.Debug("engage", $"Engage attempt: weapon='{WeaponCaption(driver.Turrets.SelectedStation)}' "
                + $"target='{_shownTarget.unitName}' type={_shownTarget.GetType().Name} "
                + $"radarAlt={_shownTarget.radarAlt:F1} worldY={_shownTarget.transform.position.y:F1} status={solution.Status}");
            if (solution.Status != ArtillerySolver.Status.Ready) return;

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
            if (_lowRow == null || _highRow == null)
            {
                _window.Destroy();
                _window = null;
                return false;
            }

            _window.SetButtonText(_window.Confirm, "ENGAGE");
            _window.Confirm.onClick.AddListener(Engage);
            _window.Cancel.onClick.AddListener(CancelOrCeaseFire);
            return true;
        }
    }
}
