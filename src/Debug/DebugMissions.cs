using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NuclearOption.Networking;
using NuclearOption.SavedMission;
using NuclearOption.SceneLoading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlayableVehicles
{
    internal static class DebugMissions
    {
        private const float MenuWaitSeconds = 60f;
        private static bool _loading;

        public static bool MenuLoaded => MainMenu.State == MainMenu.LoadingState.Loaded;

        private static bool NetworkActive
        {
            get
            {
                NetworkManagerNuclearOption manager = NetworkManagerNuclearOption.i;
                return manager != null && ((manager.Server != null && manager.Server.Active) || (manager.Client != null && manager.Client.Active));
            }
        }

        public static void List(DebugServer.Request request)
        {
            MissionGroup.Init();
            string filter = request.Arg("filter", "");
            var list = new List<object>();
            foreach (MissionKey key in MissionGroup.All.GetMissions())
            {
                if (filter.Length > 0 && key.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0
                    && key.Key.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                bool? singlePlayer = IsSinglePlayer(key);
                list.Add(new Dictionary<string, object>
                {
                    ["name"] = key.Name,
                    ["key"] = key.Key,
                    ["group"] = key.Group != null ? key.Group.Name : null,
                    ["singlePlayer"] = singlePlayer.HasValue ? (object)singlePlayer.Value : null,
                });
            }
            request.Reply(new Dictionary<string, object> { ["ok"] = true, ["count"] = list.Count, ["missions"] = list });
        }

        private static bool? IsSinglePlayer(MissionKey key)
        {
            try
            {
                if (!key.TryQuickLoad(out MissionQuickLoad quick, out _)) return null;
                List<MissionTag> tags = quick.missionSettings.Tags;
                return tags != null && tags.Any(tag => tag.Equals(MissionTag.SinglePlayer));
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void Load(DebugServer.Request request)
        {
            string name = request.Arg("name", "");
            if (name.Length == 0)
            {
                request.Fail("name is required (use 'missions' for the list)");
                return;
            }
            if (_loading)
            {
                request.Fail("a mission is already being started");
                return;
            }
            if (NetworkManagerNuclearOption.i == null || !MenuLoaded)
            {
                request.Fail("main menu is still loading (MainMenu.State != Loaded), try again in a few seconds");
                return;
            }

            if (!FindMission(name, request.Arg("group", ""), out MissionKey key, out string error))
            {
                request.Fail(error);
                return;
            }
            if (request.Arg("force", "0") != "1" && IsSinglePlayer(key) == false)
            {
                request.Fail("mission '" + key.Name + "' is not tagged Single Player (add force=1 to start it anyway)");
                return;
            }
            if (!key.TryLoad(out Mission mission, out string loadError))
            {
                request.Fail("failed to load mission '" + key.Name + "': " + loadError);
                return;
            }

            bool stopFirst = NetworkActive;
            ModLog.Info("Debug: starting mission '" + key.Name + "' [" + (key.Group != null ? key.Group.Name : "?") + "]" + (stopFirst ? " (leaving the current one first)" : ""));
            _loading = true;
            DebugServer.Host.StartCoroutine(LoadRoutine(mission, stopFirst));
            request.Reply(new Dictionary<string, object>
            {
                ["ok"] = true,
                ["message"] = "started",
                ["mission"] = key.Name,
                ["group"] = key.Group != null ? key.Group.Name : null,
                ["restart"] = stopFirst,
            });
        }

        private static bool FindMission(string text, string group, out MissionKey found, out string error)
        {
            MissionGroup.Init();
            found = default;
            error = null;
            var exact = new List<MissionKey>();
            var partial = new List<MissionKey>();
            foreach (MissionKey key in MissionGroup.All.GetMissions())
            {
                if (group.Length > 0 && (key.Group == null || !string.Equals(key.Group.Name, group, StringComparison.OrdinalIgnoreCase))) continue;
                if (string.Equals(key.Name, text, StringComparison.OrdinalIgnoreCase) || string.Equals(key.Key, text, StringComparison.OrdinalIgnoreCase))
                    exact.Add(key);
                else if (key.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                    partial.Add(key);
            }
            List<MissionKey> pool = exact.Count > 0 ? exact : partial;
            if (pool.Count == 1)
            {
                found = pool[0];
                return true;
            }
            if (pool.Count == 0)
            {
                error = "no mission matches '" + text + "' (use 'missions' for the list)";
                return false;
            }
            error = "'" + text + "' is ambiguous (" + pool.Count + "): "
                + string.Join("; ", pool.Take(8).Select(key => key.Name + " [" + (key.Group != null ? key.Group.Name : "?") + "]"))
                + " - give the exact name or a group";
            return false;
        }

        private static IEnumerator LoadRoutine(Mission mission, bool stopFirst)
        {
            try
            {
                if (stopFirst)
                {
                    if (VehicleSession.IsActive) VehicleSession.Toggle();
                    NetworkManagerNuclearOption.i.Stop(true);
                }
            }
            catch (Exception e)
            {
                ModLog.Warning("Debug: leaving the mission failed: " + e.Message);
                _loading = false;
                yield break;
            }

            if (stopFirst)
            {
                float deadline = Time.realtimeSinceStartup + MenuWaitSeconds;
                while (Time.realtimeSinceStartup < deadline && !MainMenuReached()) yield return null;
                if (!MainMenuReached())
                {
                    ModLog.Warning("Debug: the main menu did not appear within " + MenuWaitSeconds + " s, mission not started");
                    _loading = false;
                    yield break;
                }
                yield return new WaitForSecondsRealtime(1f);
            }

            StartHost(mission);
            _loading = false;
        }

        private static bool MainMenuReached()
        {
            return !NetworkActive && !MissionManager.IsRunning && MenuLoaded && (SceneManager.GetActiveScene().path == MapLoader.MainMenu || SceneManager.GetSceneByPath(MapLoader.MainMenu).isLoaded);
        }

        private static void StartHost(Mission mission)
        {
            try
            {
                MissionManager.SetMission(mission, false);
                NetworkManagerNuclearOption.i.StartHost(new HostOptions(SocketType.Offline, GameState.SinglePlayer, mission.MapKey));
            }
            catch (Exception e)
            {
                ModLog.Warning("Debug: StartHost failed: " + e);
            }
        }

        public static void Quit(DebugServer.Request request)
        {
            request.Reply(new Dictionary<string, object> { ["ok"] = true, ["message"] = "quitting" });
            DebugServer.Host.StartCoroutine(QuitRoutine());
        }

        private static IEnumerator QuitRoutine()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            ModLog.Info("Debug: quit requested");
            Application.Quit();
        }
    }
}
