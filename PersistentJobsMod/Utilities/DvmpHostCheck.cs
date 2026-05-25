using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;

namespace PersistentJobsMod.Utilities {
    /// <summary>
    /// Determines whether this game instance is running as a multiplayer client (not the host).
    /// Uses reflection against DVMP (mod ID "Multiplayer") so PJ has no hard compile-time
    /// dependency on DVMP — PJ still runs fine in singleplayer or when DVMP is absent.
    /// </summary>
    internal static class DvmpHostCheck {
        // Cached once on first call. null means DVMP not found or reflection failed.
        private static MethodInfo _isHostMethod;
        private static bool _resolved;

        private static void Resolve() {
            if (_resolved) return;
            _resolved = true;

            try {
                var dvmpMod = UnityModManager.modEntries
                    .FirstOrDefault(m => m.Info.Id == "Multiplayer" && m.Active && !m.ErrorOnLoading);

                if (dvmpMod?.Assembly == null) {
                    Main._modEntry.Logger.Log("[PJ] DVMP not found — assuming singleplayer");
                    return;
                }

                var lifecycleType = dvmpMod.Assembly
                    .GetType("Multiplayer.Components.Networking.NetworkLifecycle");

                if (lifecycleType == null) {
                    Main._modEntry.Logger.Warning("[PJ] DVMP found but NetworkLifecycle type missing — assuming host");
                    return;
                }

                _isHostMethod = lifecycleType.GetMethod(
                    "IsHost",
                    BindingFlags.Public | BindingFlags.Instance,
                    null, Type.EmptyTypes, null);

                if (_isHostMethod == null)
                    Main._modEntry.Logger.Warning("[PJ] NetworkLifecycle.IsHost() not found — assuming host");
                else
                    Main._modEntry.Logger.Log("[PJ] DVMP host check hooked successfully");
            }
            catch (Exception e) {
                Main._modEntry.Logger.Warning($"[PJ] DVMP host check setup failed: {e.Message}");
            }
        }

        /// <summary>
        /// Returns true if DVMP is active and this instance is a pure client (not hosting).
        /// Returns false in singleplayer or when this instance is the host.
        /// </summary>
        public static bool IsMultiplayerClient() {
            Resolve();
            if (_isHostMethod == null) return false;  // no DVMP or reflection failed → proceed

            try {
                // NetworkLifecycle is a SingletonBehaviour — find the live instance in the scene
                var instance = UnityEngine.Object.FindObjectOfType(_isHostMethod.DeclaringType);
                if (instance == null) return false;  // DVMP not yet fully initialized

                bool isHost = (bool)_isHostMethod.Invoke(instance, null);
                return !isHost;
            }
            catch (Exception e) {
                Main._modEntry.Logger.Warning($"[PJ] IsMultiplayerClient check failed: {e.Message}");
                return false;  // fail-safe: proceed as if singleplayer
            }
        }
    }
}
