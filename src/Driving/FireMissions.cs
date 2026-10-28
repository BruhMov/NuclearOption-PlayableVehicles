using System.Collections.Generic;
using UnityEngine;

namespace PlayableVehicles
{
    internal enum FireMissionKind
    {
        None,
        Artillery,
        Ballistic,
        AirDefense,
    }

    internal static class FireMissions
    {
        private static readonly Dictionary<MissileDefinition, MissileSeeker> _seekers = new Dictionary<MissileDefinition, MissileSeeker>();

        public static FireMissionKind KindOf(WeaponStation station)
        {
            if (station == null) return FireMissionKind.None;
            if (station.Turrets != null)
            {
                foreach (Turret turret in station.Turrets)
                {
                    if (ArtillerySolver.IsArtillery(turret)) return FireMissionKind.Artillery;
                }
            }

            MissileSeeker seeker = Seeker(Launcher(station));
            if (seeker is BallisticMissileGuidance) return FireMissionKind.Ballistic;
            if (seeker is SARHSeeker || seeker is IRSeeker || seeker is ARHSeeker) return FireMissionKind.AirDefense;
            return FireMissionKind.None;
        }

        public static MissileLauncher Launcher(WeaponStation station)
        {
            if (station == null || station.Weapons == null) return null;
            foreach (Weapon weapon in station.Weapons)
            {
                if (weapon is MissileLauncher launcher && launcher.missile != null) return launcher;
            }
            return null;
        }

        public static MissileSeeker Seeker(MissileLauncher launcher)
        {
            MissileDefinition missile = launcher != null ? launcher.missile : null;
            if (missile == null) return null;
            if (!_seekers.TryGetValue(missile, out MissileSeeker seeker))
            {
                seeker = missile.unitPrefab != null ? missile.unitPrefab.GetComponentInChildren<MissileSeeker>(true) : null;
                _seekers[missile] = seeker;
            }
            return seeker;
        }

        public static bool NeedsRadar(WeaponStation station)
        {
            return Seeker(Launcher(station)) is SARHSeeker;
        }

        // Кэш поиска радара поддержки. Раньше использовался только vehicle.radar - ссылка, которую игра
        // назначает один раз при спавне юнита. Если собственный радар SAM-а погибал, ссылка оставалась
        // "мёртвой", и новый купленный радар никогда не подхватывался: мод навсегда отдавал "No radar link".
        // Теперь каждый скан заново находит живые радары; кеш хранит результат не дольше секунды и перед
        // выдачей перепроверяет, что закешированный юнит ещё жив (стандартной Unity-проверкой, а не полем
        // isDestroyed - оно запаздывает). vehicle.radar при этом имеет тип TargetDetector (базовый класс
        // детекторов), а не Unit: юнит собственного радара берётся через detector.unit, а сам радар
        // опознаётся приведением детектора к Radar ("as Radar"), чтобы IR/LGR/SAM-детекторы не считались.
        private const float RadarLinkRadius = 300f;
        private const float RadarCacheInterval = 1f;
        private const int RadarScanMax = 64;

        private class RadarCache
        {
            public double Stamp;
            public Unit Link;
        }

        private static readonly Dictionary<uint, RadarCache> _radarLinks = new Dictionary<uint, RadarCache>();
        private static readonly List<Unit> _radarScratch = new List<Unit>(RadarScanMax);

        public static bool HasRadar(GroundVehicle vehicle)
        {
            return FindRadarLink(vehicle) != null;
        }

        // Возвращает живой радар, который может подсветить цель для этой машины: собственный радар
        // либо friendly radar / fire control vehicle в пределах RadarLinkRadius. Поиск идёт по
        // фактическим живым юнитам (не по сохранённой ссылке vehicle.radar), поэтому переживает
        // гибель старого радара и спавн нового. На клиенте проверка связи делается сервером при
        // выстреле, локально считаем, что радар есть.
        // vehicle.radar имеет тип TargetDetector (базовый класс детекторов), а не Unit - передаём
        // его компонент напрямую, а юнит берём через detector.unit.
        public static Unit FindRadarLink(GroundVehicle vehicle)
        {
            if (vehicle == null) return null;
            if (!vehicle.IsServer) return vehicle;

            uint key = vehicle.persistentID.Id;
            double now = Time.realtimeSinceStartupAsDouble;
            if (_radarLinks.TryGetValue(key, out RadarCache cache) && now - cache.Stamp < RadarCacheInterval)
                return LiveRadarUnit(cache.Link);

            Unit best = ClosestRadar(vehicle);
            _radarLinks[key] = new RadarCache { Stamp = now, Link = best };
            return best;
        }

        private static Unit ClosestRadar(GroundVehicle vehicle)
        {
            Vector3 origin = vehicle.transform.position;
            float bestSqr = RadarLinkRadius * RadarLinkRadius;
            // Собственный радар машины проверяется по компоненту vehicle.radar (TargetDetector):
            // он подхватывается первым, даже если игра ещё не обновила связку после респавна.
            Unit best = LiveRadarUnit(vehicle, OwnRadarUnit(vehicle), vehicle.radar, origin, ref bestSqr);

            FactionHQ hq = vehicle.NetworkHQ;
            _radarScratch.Clear();
            if (hq != null)
            {
                foreach (PersistentID id in hq.trackingDatabase.Keys)
                {
                    if (_radarScratch.Count >= RadarScanMax) break;
                    if (!UnitRegistry.TryGetUnit(new PersistentID?(id), out Unit unit)) continue;
                    _radarScratch.Add(unit);
                }
            }
            else
            {
                // Одиночная игра без реестра фракции: проход по всем юнитам с ограничением по количеству.
                List<Unit> units = UnitRegistry.allUnits;
                for (int i = 0; i < units.Count && _radarScratch.Count < RadarScanMax; i++)
                {
                    if (units[i] is GroundVehicle) _radarScratch.Add(units[i]);
                }
            }

            for (int i = 0; i < _radarScratch.Count; i++)
            {
                Unit candidate = _radarScratch[i];
                if (candidate == vehicle) continue;
                best = LiveRadarUnit(vehicle, candidate, null, origin, ref bestSqr) ?? best;
            }

            ModLog.Debug("radarlink", $"Radar scan for '{vehicle.unitName}': own=" +
                $"{(OwnRadarUnit(vehicle) != null ? OwnRadarUnit(vehicle).unitName : "none")} " +
                $"hq={(vehicle.NetworkHQ != null ? "tracking" : "allUnits")} " +
                $"scanned={_radarScratch.Count} result=" +
                $"{(best != null ? $"'{best.unitName}' at {FlatDistance(best, origin):F0} m" : "NONE (no live friendly Radar within 300 m)")}");
            return best;
        }

        private static float FlatDistance(Unit u, Vector3 origin)
        {
            if (u == null) return float.MaxValue;
            Vector3 d = u.transform.position - origin;
            d.y = 0f;
            return d.magnitude;
        }

        private static Unit OwnRadarUnit(GroundVehicle vehicle)
        {
            TargetDetector detector = vehicle.radar;
            if (detector == null) return null;
            return detector.unit != null ? detector.unit : detector.GetComponentInParent<Unit>();
        }

        // Живой ли это радар поддержки для данной машины? hint - уже известный детектор кандидата
        // (для собственной машины это vehicle.radar). Тип поля - TargetDetector, базовый класс всех
        // детекторов игры, а не Radar: поэтому приводим через "as Radar" - у IR/LGR/SAM-детекторов
        // он даст null и кандидат не пройдёт как радар. Для кандидатов из реестра, где детектор
        // неизвестен, ищем компонент Radar напрямую.
        // Полю "мёртвости" здесь доверять нельзя: у только что подбитого юнита оно ещё может быть
        // false, а C#-wrapper кэша переживает уничтожение GameObject. Надёжнее стандартная
        // Unity-проверка: MonoBehaviour == null становится true после Destroy, а bool-каст
        // дополнительно ловит выключенный/неактивный объект; disabled - флаг гибели юнита в игре.
        private static Unit LiveRadarUnit(GroundVehicle vehicle, Unit candidate, TargetDetector hint, Vector3 origin, ref float bestSqr)
        {
            if (candidate == null || ReferenceEquals(candidate, vehicle)) return null;
            if (!(bool)candidate) return null;              // Unity-проверка: destroyed object или inactive GameObject
            if (candidate.disabled) return null;            // игра отключает юнит при гибели, до фактического удаления

            Radar radar = hint as Radar;
            if (radar == null && hint == null) radar = candidate.GetComponent<Radar>();
            if (radar == null || !(bool)radar) return null; // MonoBehaviour null + isActiveAndEnabled через bool-каст

            if (vehicle.NetworkHQ != null && candidate.NetworkHQ != vehicle.NetworkHQ) return null;

            Vector3 delta = candidate.transform.position - origin;
            delta.y = 0f;
            float sqr = delta.sqrMagnitude;
            if (sqr > bestSqr) return null;
            bestSqr = sqr;
            return candidate;
        }

        // Быстрая проверка кэшированной ссылки между пересканами (без повторного сканирования):
        // тот же самый юнит ещё жив. Проверки те же, что в основном варианте, но без детектора-
        // подсказки и без фильтра по фракции/расстоянию - позиция радара за секунду не уйдёт
        // дальше 300 м, а смену фракции ловит следующий полный скан.
        private static Unit LiveRadarUnit(Unit cached)
        {
            if (cached == null) return null;
            if (!(bool)cached) return null;
            if (cached.disabled) return null;
            Radar radar = cached.GetComponent<Radar>();
            if (radar == null || !(bool)radar) return null;
            return cached;
        }

        public static ArtillerySolver.Solution Check(FireMissionKind kind, GroundVehicle vehicle, WeaponStation station, Unit target)
        {
            var solution = new ArtillerySolver.Solution { Status = ArtillerySolver.Status.OutOfRange };
            WeaponInfo info = station != null ? station.WeaponInfo : null;
            if (vehicle == null || info == null || target == null) return solution;

            TargetRequirements req = info.targetRequirements;
            Vector3 origin = vehicle.transform.position;
            Vector3 point = TargetLock.AimPoint(target);
            Vector3 toTarget = point - origin;
            solution.Direction = toTarget;

            if (kind == FireMissionKind.Ballistic)
            {
                solution.Range = new Vector3(toTarget.x, 0f, toTarget.z).magnitude;
                solution.Status = Within(solution.Range, req);
                return solution;
            }

            solution.Range = toTarget.magnitude;
            // Раздельная проверка: раньше тип цели и её высота были в одном условии, и отказ по высоте
            // ("radarAlt < 1") выглядел как "цель не самолёт". Отладочный лог пишет фактический тип цели
            // (через GetType(), а не is Aircraft), radarAlt и мировую Y - чтобы отличить отказ по типу от отказа по высоте.
            bool isAirType = target is Aircraft || target is Missile;
            float minAltitude = Mathf.Max(req.minAltitude, 1f);
            if (!isAirType || target.radarAlt < minAltitude)
            {
                ModLog.Debug("aircheck", $"Air target rejected: name='{target.unitName}' type={target.GetType().Name} "
                    + $"isAirType={isAirType} radarAlt={target.radarAlt:F1} worldY={target.transform.position.y:F1} "
                    + $"reqMinAltitude={req.minAltitude:F1} floor={minAltitude:F1} range={solution.Range:F0} kind={kind}");
                solution.Status = ArtillerySolver.Status.NotAirborne;
                return solution;
            }
            if (req.maxAltitude > 0f && target.radarAlt > req.maxAltitude)
            {
                ModLog.Debug("aircheck-high", $"Air target too high: name='{target.unitName}' type={target.GetType().Name} "
                    + $"radarAlt={target.radarAlt:F1} maxAltitude={req.maxAltitude:F1}");
                solution.Status = ArtillerySolver.Status.AltitudeLimit;
                return solution;
            }
            solution.Status = Within(solution.Range, req);
            if (solution.Status != ArtillerySolver.Status.Ready) return solution;

            if (NeedsRadar(station) && !HasRadar(vehicle)) solution.Status = ArtillerySolver.Status.NoRadar;
            else if (req.lineOfSight && !TerrainClear(origin + Vector3.up * 3f, target)) solution.Status = ArtillerySolver.Status.NoLineOfSight;
            return solution;
        }

        private static bool TerrainClear(Vector3 origin, Unit target)
        {
            Vector3 point = target.transform.position;
            return !Physics.Linecast(origin, point, out RaycastHit hit, PhysicsLayers.StaticsMask)
                || FastMath.InRange(hit.point, point, target.maxRadius * 2f);
        }

        private static ArtillerySolver.Status Within(float range, in TargetRequirements req)
        {
            if (req.maxRange > 0f && range > req.maxRange) return ArtillerySolver.Status.OutOfRange;
            if (req.minRange > 0f && range < req.minRange) return ArtillerySolver.Status.TooClose;
            return ArtillerySolver.Status.Ready;
        }
    }
}
