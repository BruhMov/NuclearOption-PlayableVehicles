using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace PlayableVehicles
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    internal sealed class OptionalPatchAttribute : Attribute
    {
        public string Feature { get; }

        public OptionalPatchAttribute(string feature)
        {
            Feature = feature;
        }
    }

    internal static class PatchLoader
    {
        public static bool ApplyAll(Harmony harmony)
        {
            int applied = 0;
            var skipped = new List<string>();
            var optionalFailed = new List<string>();

            foreach (Type type in AccessTools.GetTypesFromAssembly(typeof(PatchLoader).Assembly))
            {
                if (!type.IsDefined(typeof(HarmonyAttribute), false)) continue;

                OptionalPatchAttribute optional = null;
                try
                {
                    optional = type.GetCustomAttribute<OptionalPatchAttribute>(false);
                    List<MethodInfo> replacements = harmony.CreateClassProcessor(type).Patch();
                    if (replacements == null || replacements.Count == 0) skipped.Add(type.Name);
                    else applied++;
                }
                catch (Exception e)
                {
                    if (optional != null)
                    {
                        ModLog.Warning("Optional patch " + type.Name + " failed, feature off (" + optional.Feature + "): " + e);
                        optionalFailed.Add(type.Name);
                        RemoveClassPatches(harmony, type);
                        continue;
                    }
                    ModLog.Error("Critical patch " + type.Name + " failed - game was probably updated. "
                        + "All patches removed, mod disabled: " + e);
                    try
                    {
                        harmony.UnpatchSelf();
                    }
                    catch (Exception unpatchError)
                    {
                        ModLog.Error("UnpatchSelf failed: " + unpatchError);
                    }
                    return false;
                }
            }

            ModLog.Info("Harmony: " + applied + " patch classes applied"
                + (skipped.Count > 0 ? ", skipped by Prepare: " + string.Join(", ", skipped) : "")
                + (optionalFailed.Count > 0 ? ", optional failed: " + string.Join(", ", optionalFailed) : ""));
            return true;
        }

        private static void RemoveClassPatches(Harmony harmony, Type patchClass)
        {
            try
            {
                var originals = new List<MethodBase>(Harmony.GetAllPatchedMethods());
                foreach (MethodBase original in originals)
                {
                    Patches info = Harmony.GetPatchInfo(original);
                    if (info == null) continue;
                    RemoveOwn(harmony, original, info.Prefixes, patchClass);
                    RemoveOwn(harmony, original, info.Postfixes, patchClass);
                    RemoveOwn(harmony, original, info.Transpilers, patchClass);
                    RemoveOwn(harmony, original, info.Finalizers, patchClass);
                }
            }
            catch (Exception e)
            {
                ModLog.Warning("Cleanup of failed patch " + patchClass.Name + " failed: " + e);
            }
        }

        private static void RemoveOwn(Harmony harmony, MethodBase original, IEnumerable<Patch> patches, Type patchClass)
        {
            if (patches == null) return;
            foreach (Patch patch in new List<Patch>(patches))
            {
                if (patch.owner == harmony.Id && patch.PatchMethod != null && patch.PatchMethod.DeclaringType == patchClass)
                {
                    harmony.Unpatch(original, patch.PatchMethod);
                }
            }
        }

        public static IEnumerable<MethodBase> DeclaredMethods(Type type, string patchName, params string[] names)
        {
            var found = new List<MethodBase>();
            foreach (string name in names)
            {
                MethodInfo method = AccessTools.DeclaredMethod(type, name);
                if (method != null) found.Add(method);
                else ModLog.Warning(patchName + ": " + type.Name + "." + name + " not found - not patched");
            }
            return found;
        }
    }
}
