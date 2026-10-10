using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using NuclearOption.Networking;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class DebugCommands
    {
        private const float MaxDriveSeconds = 30f;

        public static void Execute(DebugServer.Request request)
        {
            switch (request.Command)
            {
                case "":
                case "ping":
                    request.Reply(new Dictionary<string, object> { ["ok"] = true, ["version"] = Plugin.Version, ["mission"] = MissionManager.IsRunning, ["menuLoaded"] = DebugMissions.MenuLoaded });
                    break;
                case "state":
                    request.Reply(State());
                    break;
                case "enter":
                    Enter(request);
                    break;
                case "exit":
                    if (!VehicleSession.IsActive)
                    {
                        request.Fail("not in a vehicle");
                        return;
                    }
                    VehicleSession.Toggle();
                    request.Reply(Ok("exited"));
                    break;
                case "drive":
                    Drive(request);
                    break;
                case "action":
                    Action(request);
                    break;
                case "buy":
                    Buy(request);
                    break;
                case "screenshot":
                    DebugServer.Host.StartCoroutine(Screenshot(request));
                    break;
                case "uipick":
                    UiPick(request);
                    break;
                case "physpick":
                    PhysPick(request);
                    break;
                case "look":
                    if (!VehicleSession.IsActive || VehicleSession.ViewCamera == null)
                    {
                        request.Fail("not in a vehicle");
                        return;
                    }
                    if (uint.TryParse(request.Arg("unit", ""), out uint lookId) && UnitRegistry.TryGetUnit(new PersistentID?(new PersistentID { Id = lookId }), out Unit lookUnit))
                    {
                        Vector3 to = lookUnit.transform.position - VehicleSession.Vehicle.transform.position;
                        float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                        float pitch = -Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
                        VehicleSession.ViewCamera.DebugLook(yaw, pitch);
                        request.Reply(Ok("looking at " + lookUnit.unitName + " (" + to.magnitude.ToString("F0") + " m)"));
                        return;
                    }
                    VehicleSession.ViewCamera.DebugLook(request.Float("yaw", VehicleSession.Vehicle.transform.eulerAngles.y), request.Float("pitch", 5f));
                    request.Reply(Ok("camera turned"));
                    break;
                case "click":
                    Click(request);
                    break;
                case "select":
                    Select(request);
                    break;
                case "park":
                    Park(request);
                    break;
                case "unitinfo":
                    UnitInfo(request);
                    break;
                case "missions":
                    DebugMissions.List(request);
                    break;
                case "loadmission":
                    DebugMissions.Load(request);
                    break;
                case "quit":
                    DebugMissions.Quit(request);
                    break;
                default:
                    request.Fail("unknown command '" + request.Command + "'. Commands: ping, state, enter, exit, drive, action, buy, screenshot, uipick, physpick, look, click, select, park, unitinfo, missions, loadmission, quit");
                    break;
            }
        }

        private static Dictionary<string, object> Ok(string message)
        {
            return new Dictionary<string, object> { ["ok"] = true, ["message"] = message };
        }

        private static Dictionary<string, object> State()
        {
            var state = new Dictionary<string, object>
            {
                ["ok"] = true,
                ["mission"] = MissionManager.IsRunning,
                ["paused"] = GameplayUI.GameIsPaused,
                ["timeScale"] = Time.timeScale,
                ["network"] = NetBridge.IsClientOnly ? "client" : NetBridge.IsServer ? "host" : "offline",
                ["mapMaximized"] = DynamicMap.mapMaximized,
                ["fireControlOpen"] = FireControlPanel.IsOpen,
                ["vehicleMenuOpen"] = VehicleSelectionMenu.IsOpen,
                ["debugZoom"] = DebugInput.Zoom,
            };

            if (GameManager.GetLocalPlayer(out Player player))
            {
                state["player"] = new Dictionary<string, object>
                {
                    ["name"] = player.GetDisplayName(PlayerNameContext.Other),
                    ["allocation"] = player.Allocation,
                    ["faction"] = player.HQ != null && player.HQ.faction != null ? player.HQ.faction.factionName : null,
                };
            }
            if (GameManager.GetLocalAircraft(out Aircraft aircraft) && aircraft != null)
            {
                state["aircraft"] = new Dictionary<string, object>
                {
                    ["name"] = aircraft.unitName,
                    ["disabled"] = aircraft.disabled,
                    ["position"] = aircraft.GlobalPosition().AsVector3(),
                    ["speed"] = aircraft.speed,
                    ["radarAlt"] = aircraft.radarAlt,
                };
            }

            CameraStateManager camera = SceneSingleton<CameraStateManager>.i;
            if (camera != null)
            {
                state["camera"] = new Dictionary<string, object>
                {
                    ["state"] = camera.currentState != null ? camera.currentState.GetType().Name : null,
                    ["following"] = camera.followingUnit != null ? camera.followingUnit.unitName : null,
                    ["position"] = camera.transform.position.ToGlobalPosition().AsVector3(),
                };
            }

            state["session"] = VehicleSession.IsActive ? Session() : null;
            return state;
        }

        private static Dictionary<string, object> Session()
        {
            GroundVehicle vehicle = VehicleSession.Vehicle;
            VehicleDriver driver = VehicleSession.Driver;
            var session = new Dictionary<string, object>
            {
                ["mode"] = driver != null ? driver.Mode.ToString() : null,
                ["static"] = driver != null && driver.Static,
            };
            if (vehicle == null) return session;

            Rigidbody rb = vehicle.rb;
            Vector3 euler = vehicle.transform.eulerAngles;
            session["vehicle"] = vehicle.unitName;
            session["id"] = vehicle.persistentID.Id;
            session["disabled"] = vehicle.disabled;
            session["position"] = vehicle.GlobalPosition().AsVector3();
            session["speed"] = vehicle.speed;
            session["velocity"] = rb != null ? rb.velocity.magnitude : 0f;
            session["radarAlt"] = vehicle.radarAlt;
            session["pitch"] = Signed(euler.x);
            session["roll"] = Signed(euler.z);
            session["heading"] = euler.y;
            session["deployed"] = VehicleDeploy.IsDeployed(vehicle);
            TowHitch hitch = Towing.HitchOf(vehicle);
            session["towing"] = hitch != null && hitch.Trailer != null ? hitch.Trailer.unitName + " [" + hitch.Trailer.persistentID.Id + "]" : null;
            CargoMount cargo = Cargo.LoadOf(vehicle);
            session["cargo"] = cargo != null && cargo.Container != null ? cargo.Container.unitName + " [" + cargo.Container.persistentID.Id + "]" : null;
            session["kinematic"] = rb != null && rb.isKinematic;

            if (driver != null)
            {
                session["throttle"] = driver.Throttle;
                session["steer"] = driver.Steer;
                session["physics"] = driver.UsesVanillaPhysics ? "vanilla" : "arcade";
                if (driver.UsesVanillaPhysics)
                {
                    session["grounded"] = driver.Vanilla.Grounded;
                    session["onPaved"] = driver.Vanilla.OnPaved;
                    session["topSpeed"] = driver.Vanilla.TopSpeed;
                }

                TurretController turrets = driver.Turrets;
                WeaponStation station = turrets.SelectedStation;
                session["station"] = new Dictionary<string, object>
                {
                    ["index"] = turrets.SelectedStationIndex,
                    ["weapon"] = station != null && station.WeaponInfo != null ? station.WeaponInfo.weaponName : null,
                    ["ammo"] = station != null ? station.Ammo : 0,
                    ["fullAmmo"] = station != null ? station.FullAmmo : 0,
                    ["fireMission"] = turrets.MissionKind.ToString(),
                    ["proximityFuse"] = TurretController.HasProximityFuse(station),
                };
                session["lockedTarget"] = turrets.LockedTarget != null ? turrets.LockedTarget.unitName : null;
                session["fuseTarget"] = turrets.FuseTargetDebug != null ? turrets.FuseTargetDebug.unitName : null;
                session["mission"] = turrets.MissionActive
                    ? new Dictionary<string, object> { ["target"] = turrets.MissionTarget.unitName, ["mode"] = turrets.MissionMode.ToString() }
                    : null;
            }
            return session;
        }

        private static float Signed(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }

        private static void Enter(DebugServer.Request request)
        {
            if (VehicleSession.IsActive)
            {
                request.Fail("already in " + VehicleSession.Vehicle?.unitName);
                return;
            }
            string target = request.Arg("target", "nearest");
            if (target == "selected")
            {
                VehicleSession.Toggle();
                request.Reply(Ok("toggle sent (selected vehicle, depot menu or nothing)"));
                return;
            }

            GroundVehicle vehicle = uint.TryParse(target, out uint id) ? FindById(id) : FindNearestOwn();
            if (vehicle == null)
            {
                request.Fail("no vehicle found for target '" + target + "'");
                return;
            }
            VehicleSession.EnterDeployed(vehicle);
            request.Reply(Ok("entering " + vehicle.unitName + " [" + vehicle.persistentID.Id + "]"));
        }

        private static GroundVehicle FindById(uint id)
        {
            foreach (GroundVehicle vehicle in Object.FindObjectsOfType<GroundVehicle>())
            {
                if (vehicle.persistentID.Id == id && !vehicle.disabled) return vehicle;
            }
            return null;
        }

        private static GroundVehicle FindNearestOwn()
        {
            if (!GameManager.GetLocalHQ(out FactionHQ hq)) return null;
            Vector3 from = ReferencePosition();
            GroundVehicle best = null;
            float bestDistance = float.MaxValue;
            foreach (GroundVehicle vehicle in Object.FindObjectsOfType<GroundVehicle>())
            {
                if (vehicle.disabled || vehicle.NetworkHQ != hq) continue;
                float distance = (vehicle.transform.position - from).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = vehicle;
                }
            }
            return best;
        }

        private static Vector3 ReferencePosition()
        {
            if (VehicleSession.Vehicle != null) return VehicleSession.Vehicle.transform.position;
            CameraStateManager camera = SceneSingleton<CameraStateManager>.i;
            return camera != null ? camera.transform.position : Vector3.zero;
        }

        private static void Drive(DebugServer.Request request)
        {
            VehicleDriver driver = VehicleSession.IsActive ? VehicleSession.Driver : null;
            if (driver == null || VehicleSession.Vehicle == null)
            {
                request.Fail("not in a vehicle");
                return;
            }
            float throttle = Mathf.Clamp(request.Float("throttle", 0f), -1f, 1f);
            float steer = Mathf.Clamp(request.Float("steer", 0f), -1f, 1f);
            float seconds = Mathf.Clamp(request.Float("seconds", 3f), 0.1f, MaxDriveSeconds);
            float interval = Mathf.Clamp(request.Float("interval", 0.25f), 0.02f, 5f);
            bool fire = request.Arg("fire", "0") == "1";
            DebugServer.Host.StartCoroutine(DriveRoutine(request, throttle, steer, seconds, interval, fire));
        }

        private static IEnumerator DriveRoutine(DebugServer.Request request, float throttle, float steer, float seconds, float interval, bool fire)
        {
            var samples = new List<object>();
            float start = Time.realtimeSinceStartup;
            float nextSample = start;
            float maxSpeed = 0f;
            GroundVehicle vehicle = VehicleSession.Vehicle;
            Vector3 startPosition = vehicle.transform.position;
            DebugInput.Set(throttle, steer, fire);
            int startAmmo = VehicleSession.Driver != null && VehicleSession.Driver.Turrets.SelectedStation != null
                ? VehicleSession.Driver.Turrets.SelectedStation.Ammo : 0;
            try
            {
                while (Time.realtimeSinceStartup - start < seconds && VehicleSession.IsActive && VehicleSession.Vehicle == vehicle && !vehicle.disabled)
                {
                    float now = Time.realtimeSinceStartup;
                    if (now >= nextSample)
                    {
                        nextSample = now + interval;
                        maxSpeed = Mathf.Max(maxSpeed, Mathf.Abs(vehicle.speed));
                        samples.Add(Sample(vehicle, now - start));
                    }
                    yield return null;
                }
            }
            finally
            {
                DebugInput.Clear();
            }

            bool alive = vehicle != null && !vehicle.disabled;
            if (alive) samples.Add(Sample(vehicle, Time.realtimeSinceStartup - start));
            request.Reply(new Dictionary<string, object>
            {
                ["ok"] = alive && VehicleSession.Vehicle == vehicle,
                ["vehicle"] = alive ? vehicle.unitName : null,
                ["maxSpeed"] = maxSpeed,
                ["shots"] = alive && VehicleSession.Driver != null && VehicleSession.Driver.Turrets.SelectedStation != null
                    ? startAmmo - VehicleSession.Driver.Turrets.SelectedStation.Ammo : 0,
                ["distance"] = alive ? Vector3.Distance(startPosition, vehicle.transform.position) : 0f,
                ["samples"] = samples,
            });
        }

        private static Dictionary<string, object> Sample(GroundVehicle vehicle, float time)
        {
            Vector3 euler = vehicle.transform.eulerAngles;
            VehicleDriver driver = VehicleSession.Driver;
            var sample = new Dictionary<string, object>
            {
                ["t"] = time,
                ["speed"] = vehicle.speed,
                ["alt"] = vehicle.radarAlt,
                ["pitch"] = Signed(euler.x),
                ["roll"] = Signed(euler.z),
                ["heading"] = euler.y,
                ["position"] = vehicle.GlobalPosition().AsVector3(),
            };
            if (driver != null && driver.UsesVanillaPhysics)
            {
                sample["grounded"] = driver.Vanilla.Grounded;
                sample["paved"] = driver.Vanilla.OnPaved;
            }
            return sample;
        }

        private static void Action(DebugServer.Request request)
        {
            string name = request.Arg("name", "");
            switch (name)
            {
                case "deploy":
                    if (!VehicleSession.IsActive || VehicleSession.Driver == null)
                    {
                        request.Fail("not in a vehicle");
                        return;
                    }
                    if (Towing.IsTractor(VehicleSession.Vehicle)) Towing.Toggle(VehicleSession.Vehicle, VehicleSession.Driver);
                    else if (Cargo.IsFlatbed(VehicleSession.Vehicle)) Cargo.Toggle(VehicleSession.Vehicle, VehicleSession.Driver);
                    else VehicleDeploy.Toggle(VehicleSession.Vehicle, VehicleSession.Driver);
                    break;
                case "firecontrol":
                    FireControlPanel.Toggle();
                    break;
                case "toggle":
                    VehicleSession.Toggle();
                    break;
                case "zoom":
                    DebugInput.Zoom = !DebugInput.Zoom;
                    break;
                case "lock":
                case "weapon":
                    if (!VehicleSession.IsActive || VehicleSession.Driver == null)
                    {
                        request.Fail("not in a vehicle");
                        return;
                    }
                    if (name == "lock") VehicleSession.Driver.Turrets.DebugSelectTarget();
                    else VehicleSession.Driver.Turrets.DebugNextWeapon();
                    break;
                case "map":
                    DynamicMap map = SceneSingleton<DynamicMap>.i;
                    if (map == null)
                    {
                        request.Fail("no map");
                        return;
                    }
                    if (DynamicMap.mapMaximized) map.Minimize();
                    else map.Maximize();
                    break;
                default:
                    request.Fail("unknown action '" + name + "'. Actions: deploy, firecontrol, toggle, map, zoom, lock, weapon");
                    return;
            }
            request.Reply(Ok(name + " done"));
        }

        private static void Buy(DebugServer.Request request)
        {
            string query = request.Arg("vehicle", "");
            if (!VehicleShop.Enabled || !GameManager.GetLocalHQ(out FactionHQ hq))
            {
                request.Fail("vehicle shop not available");
                return;
            }
            VehicleDefinition definition = null;
            var names = new List<string>();
            foreach (VehicleDefinition candidate in VehicleShop.Catalog)
            {
                names.Add(candidate.unitName);
                if (definition == null && query.Length > 0 && candidate.unitName.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    definition = candidate;
                }
            }
            if (definition == null)
            {
                request.Reply(new Dictionary<string, object> { ["ok"] = false, ["error"] = "vehicle not found", ["catalog"] = names });
                return;
            }

            Vector3 from = ReferencePosition();
            VehicleDepot depot = null;
            float best = float.MaxValue;
            foreach (VehicleDepot candidate in Object.FindObjectsOfType<VehicleDepot>())
            {
                if (candidate.disabled || !VehicleShop.IsOwnDepot(candidate, hq)) continue;
                float distance = (candidate.transform.position - from).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    depot = candidate;
                }
            }
            if (depot == null)
            {
                request.Fail("no own vehicle depot");
                return;
            }

            bool deploy = request.Arg("deploy", "1") != "0";
            PurchaseClient.Request(depot, definition, deploy);
            request.Reply(Ok("purchase requested: " + definition.unitName + " at " + depot.unitName + (deploy ? ", deploy into it" : "")));
        }

        private static void UiPick(DebugServer.Request request)
        {
            var point = new Vector2(request.Float("x", 0.5f) * Screen.width, (1f - request.Float("y", 0.5f)) * Screen.height);
            var hits = new List<(int order, Dictionary<string, object> info)>();
            foreach (UnityEngine.UI.Graphic graphic in Object.FindObjectsOfType<UnityEngine.UI.Graphic>())
            {
                if (!graphic.enabled || !graphic.gameObject.activeInHierarchy || graphic.canvas == null) continue;
                Canvas canvas = graphic.canvas.rootCanvas;
                Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                if (!RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform, point, camera)) continue;

                var info = new Dictionary<string, object>
                {
                    ["path"] = HierarchyPath(graphic.transform),
                    ["type"] = graphic.GetType().Name,
                    ["canvas"] = canvas.name + " #" + canvas.sortingOrder,
                    ["alpha"] = graphic.color.a,
                    ["tint"] = ColorText(graphic.canvasRenderer.GetColor()),
                    ["color"] = ColorText(graphic.color),
                    ["animator"] = AnimatorType != null && graphic.GetComponentInParent(AnimatorType) != null,
                };
                if (graphic is TMPro.TMP_Text text)
                {
                    info["text"] = text.text;
                    info["material"] = text.fontSharedMaterial != null ? text.fontSharedMaterial.name : null;
                }
                if (graphic is UnityEngine.UI.Image image && image.sprite != null) info["sprite"] = image.sprite.name;
                if (!(graphic is TMPro.TMP_Text)) info["material"] = graphic.material != null ? graphic.material.name + " / " + graphic.material.shader.name : null;
                hits.Add((canvas.sortingOrder * 100000 + graphic.depth, info));
            }
            hits.Sort((a, b) => b.order.CompareTo(a.order));
            var list = new List<object>();
            foreach (var hit in hits) list.Add(hit.info);
            request.Reply(new Dictionary<string, object> { ["ok"] = true, ["screen"] = new Vector3(point.x, point.y, 0f), ["elements"] = list });
        }

        private static readonly System.Type AnimatorType = System.Type.GetType("UnityEngine.Animator, UnityEngine.AnimationModule");

        private static string ColorText(Color c)
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.00},{1:0.00},{2:0.00},{3:0.00}", c.r, c.g, c.b, c.a);
        }

        private static string HierarchyPath(Transform transform)
        {
            string path = transform.name;
            for (Transform parent = transform.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
            return path;
        }

        private static void Click(DebugServer.Request request)
        {
            UnityEngine.EventSystems.EventSystem events = UnityEngine.EventSystems.EventSystem.current;
            if (events == null)
            {
                request.Fail("no EventSystem");
                return;
            }
            var point = new Vector2(request.Float("x", 0.5f) * Screen.width, (1f - request.Float("y", 0.5f)) * Screen.height);
            var data = new UnityEngine.EventSystems.PointerEventData(events) { position = point, button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
            var results = new List<UnityEngine.EventSystems.RaycastResult>();
            events.RaycastAll(data, results);
            foreach (UnityEngine.EventSystems.RaycastResult result in results)
            {
                GameObject handler = UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(result.gameObject);
                if (handler == null) continue;
                data.pointerCurrentRaycast = result;
                data.pointerPressRaycast = result;
                data.pointerPress = handler;
                UnityEngine.EventSystems.ExecuteEvents.Execute(handler, data, UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
                UnityEngine.EventSystems.ExecuteEvents.Execute(handler, data, UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);
                UnityEngine.EventSystems.ExecuteEvents.Execute(handler, data, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
                request.Reply(Ok("clicked " + HierarchyPath(handler.transform)));
                return;
            }
            request.Fail("nothing clickable at this point (" + results.Count + " raycast hits)");
        }

        private static void Select(DebugServer.Request request)
        {
            string query = request.Arg("name", "");
            DynamicMap map = SceneSingleton<DynamicMap>.i;
            if (map == null || query.Length == 0)
            {
                request.Fail("need name and a map");
                return;
            }
            bool enemy = request.Arg("enemy", "0") == "1";
            GameManager.GetLocalHQ(out FactionHQ hq);
            Vector3 from = ReferencePosition();
            Unit best = null;
            float bestDistance = float.MaxValue;
            foreach (MapIcon icon in map.mapIcons)
            {
                if (!(icon is UnitMapIcon unitIcon) || unitIcon.unit == null || unitIcon.unit.disabled) continue;
                Unit unit = unitIcon.unit;
                if (unit.unitName.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (enemy && (hq == null || unit.NetworkHQ == hq || unit.NetworkHQ == null)) continue;
                float distance = (unit.transform.position - from).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = unit;
                }
            }
            if (best == null)
            {
                request.Fail("no map unit matching '" + query + "'");
                return;
            }
            if (best is VehicleDepot depot && !VehicleSession.IsActive) SpawnScreen.SelectDepot(depot);
            else
            {
                map.UnselectAll();
                map.SelectIcon(best);
            }
            request.Reply(new Dictionary<string, object>
            {
                ["ok"] = true,
                ["message"] = "selected " + best.unitName,
                ["id"] = best.persistentID.Id,
                ["distance"] = Mathf.Sqrt(bestDistance),
                ["position"] = best.GlobalPosition().AsVector3(),
                ["air"] = best is Aircraft || best is Missile,
            });
        }

        private static void Park(DebugServer.Request request)
        {
            GroundVehicle vehicle = VehicleSession.IsActive ? VehicleSession.Vehicle : null;
            if (vehicle == null || vehicle.rb == null)
            {
                request.Fail("not in a vehicle");
                return;
            }
            if (!uint.TryParse(request.Arg("unit", ""), out uint id) || !(FindById(id) is GroundVehicle trailer))
            {
                request.Fail("need unit=<trailer id>");
                return;
            }
            float gap = request.Float("gap", 1f);
            Vector3 forward = Vector3.ProjectOnPlane(trailer.transform.forward, Vector3.up).normalized;
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
            Vector3 front = trailer.transform.TransformPoint(TowHitch.FrontPoint(trailer));
            Vector3 rearLocal = TowHitch.RearPoint(vehicle);
            Vector3 position = front + forward * gap - rotation * new Vector3(rearLocal.x, 0f, rearLocal.z);
            position.y = vehicle.transform.position.y + 1f;
            vehicle.rb.velocity = Vector3.zero;
            vehicle.rb.angularVelocity = Vector3.zero;
            vehicle.rb.position = position;
            vehicle.rb.rotation = rotation;
            vehicle.transform.SetPositionAndRotation(position, rotation);
            request.Reply(Ok("parked in front of " + trailer.unitName));
        }

        private static void UnitInfo(DebugServer.Request request)
        {
            if (!uint.TryParse(request.Arg("unit", ""), out uint id) || !UnitRegistry.TryGetUnit(new PersistentID?(new PersistentID { Id = id }), out Unit unit))
            {
                request.Fail("need unit=<id>");
                return;
            }
            float groundY = float.NaN;
            if (Physics.Raycast(unit.transform.position + Vector3.up * 5f, Vector3.down, out RaycastHit ground, 30f, PhysicsLayers.StaticsMask, QueryTriggerInteraction.Ignore))
            {
                groundY = ground.point.y;
            }
            var colliders = new List<object>();
            foreach (Collider collider in unit.GetComponentsInChildren<Collider>(true))
            {
                colliders.Add(new Dictionary<string, object>
                {
                    ["name"] = collider.name,
                    ["type"] = collider.GetType().Name,
                    ["enabled"] = collider.enabled && collider.gameObject.activeInHierarchy,
                    ["trigger"] = collider.isTrigger,
                    ["bottomAboveGround"] = float.IsNaN(groundY) ? (object)null : collider.bounds.min.y - groundY,
                    ["size"] = collider.bounds.size,
                });
            }
            var info = new Dictionary<string, object>
            {
                ["ok"] = true,
                ["unit"] = unit.unitName,
                ["type"] = unit.GetType().Name,
                ["componentEnabled"] = unit.enabled,
                ["position"] = unit.GlobalPosition().AsVector3(),
                ["pivotAboveGround"] = float.IsNaN(groundY) ? (object)null : unit.transform.position.y - groundY,
                ["speed"] = unit.speed,
                ["radarAlt"] = unit.radarAlt,
                ["colliders"] = colliders,
            };
            Rigidbody body = unit.rb;
            if (body != null)
            {
                info["rigidbody"] = new Dictionary<string, object>
                {
                    ["kinematic"] = body.isKinematic,
                    ["gravity"] = body.useGravity,
                    ["mass"] = body.mass,
                    ["velocity"] = body.velocity,
                    ["sleeping"] = body.IsSleeping(),
                };
            }
            if (unit is GroundVehicle vehicle)
            {
                info["stationary"] = vehicle.NetworknetworkStationary;
                info["static"] = VehicleDriver.IsStatic(vehicle);
                info["trailer"] = VehicleGeometry.IsTrailer(vehicle);
                info["container"] = VehicleGeometry.IsContainer(vehicle);
                info["deployParts"] = GameAccess.GroundVehicleDeployableParts != null && GameAccess.GroundVehicleDeployableParts(vehicle) != null
                    ? GameAccess.GroundVehicleDeployableParts(vehicle).Length : 0;
                info["joints"] = vehicle.GetComponents<Joint>().Length;
            }
            request.Reply(info);
        }

        private static void PhysPick(DebugServer.Request request)
        {
            CameraStateManager cameraManager = SceneSingleton<CameraStateManager>.i;
            Camera camera = Camera.main;
            Vector3 origin = camera != null ? camera.transform.position : cameraManager != null ? cameraManager.transform.position : Vector3.zero;
            Vector3 end = VehicleSession.Vehicle != null && request.Arg("to", "vehicle") == "vehicle"
                ? VehicleSession.Vehicle.transform.position + Vector3.up
                : origin + (camera != null ? camera.transform.forward : Vector3.forward) * 500f;
            Vector3 direction = end - origin;
            RaycastHit[] hits = Physics.RaycastAll(origin, direction.normalized, direction.magnitude, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            var list = new List<object>();
            foreach (RaycastHit hit in hits)
            {
                Rigidbody body = hit.collider.attachedRigidbody;
                Unit unit = hit.collider.GetComponentInParent<Unit>();
                list.Add(new Dictionary<string, object>
                {
                    ["distance"] = hit.distance,
                    ["path"] = HierarchyPath(hit.collider.transform),
                    ["collider"] = hit.collider.GetType().Name,
                    ["layer"] = LayerMask.LayerToName(hit.collider.gameObject.layer) + " (" + hit.collider.gameObject.layer + ")",
                    ["trigger"] = hit.collider.isTrigger,
                    ["rigidbody"] = body != null ? (body.isKinematic ? "kinematic" : "dynamic") : null,
                    ["unit"] = unit != null ? unit.unitName : null,
                });
            }
            request.Reply(new Dictionary<string, object> { ["ok"] = true, ["length"] = direction.magnitude, ["hits"] = list,
                ["startInside"] = Physics.CheckSphere(origin, 0.1f, ~0, QueryTriggerInteraction.Ignore) });
        }

        private static IEnumerator Screenshot(DebugServer.Request request)
        {
            yield return new WaitForEndOfFrame();
            int maxWidth = Mathf.Clamp((int)request.Float("width", 1280f), 320, 3840);
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            Texture2D output = shot;
            RenderTexture rt = null;
            try
            {
                if (shot.width > maxWidth)
                {
                    int height = Mathf.RoundToInt(shot.height * (float)maxWidth / shot.width);
                    rt = RenderTexture.GetTemporary(maxWidth, height, 0);
                    Graphics.Blit(shot, rt);
                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = rt;
                    output = new Texture2D(maxWidth, height, TextureFormat.RGB24, false);
                    output.ReadPixels(new Rect(0, 0, maxWidth, height), 0, 0);
                    output.Apply();
                    RenderTexture.active = previous;
                }

                string folder = Path.Combine(Paths.BepInExRootPath, "PlayableVehicles", "debug");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "screenshot.jpg");
                File.WriteAllBytes(path, output.EncodeToJPG(85));
                request.Reply(new Dictionary<string, object> { ["ok"] = true, ["path"] = path, ["width"] = output.width, ["height"] = output.height });
            }
            finally
            {
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                if (output != shot) Object.Destroy(output);
                Object.Destroy(shot);
            }
        }
    }

    internal static class DebugInput
    {
        public static bool Active { get; private set; }
        public static float Throttle { get; private set; }
        public static float Steer { get; private set; }

        public static bool Fire { get; private set; }

        public static void Set(float throttle, float steer, bool fire = false)
        {
            Active = true;
            Throttle = throttle;
            Steer = steer;
            Fire = fire;
        }

        public static bool Zoom { get; set; }

        public static void Clear()
        {
            Active = false;
            Throttle = 0f;
            Steer = 0f;
            Fire = false;
        }
    }
}
