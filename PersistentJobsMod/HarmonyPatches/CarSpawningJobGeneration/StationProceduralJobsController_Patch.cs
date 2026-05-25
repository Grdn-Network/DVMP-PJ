using System;
using System.Linq;
using HarmonyLib;
using PersistentJobsMod.CarSpawningJobGenerators;
using PersistentJobsMod.ModInteraction;
using PersistentJobsMod.Persistence;
using UnityEngine;
using UnityModManagerNet;

namespace PersistentJobsMod.HarmonyPatches.CarSpawningJobGeneration {
    [HarmonyPatch(typeof(StationProceduralJobsController))]
    public static class StationProceduralJobsController_Patch {
        [HarmonyPatch(nameof(StationProceduralJobsController.TryToGenerateJobs))]
        [HarmonyPrefix]
        public static bool TryToGenerateJobs_Prefix(StationProceduralJobsController __instance, StationProceduralJobsRuleset ___generationRuleset, ref Coroutine ___generationCoro) {
            if (!Main._modEntry.Active) {
                return true;
            }

            // In multiplayer, only the host owns world state (job generation, car spawning).
            // Clients bail out and let the vanilla path run — it does nothing harmful since
            // the server drives all job gen. Without this guard every client independently
            // spawns its own private set of cars that only it can see.
            if (DvmpHostCheck.IsMultiplayerClient()) {
                Main._modEntry.Logger.Log($"[PJ] Skipping job gen for {__instance.stationController.logicStation.ID} — running as MP client");
                return false;
            }

            try {
                if (StationIdCarSpawningPersistence.Instance.GetHasStationSpawnedCarsFlag(__instance.stationController)) {
                    Main._modEntry.Logger.Log($"Station {__instance.stationController.logicStation.ID} has already spawned cars, skipping jobs-with-cars generation");
                } else {
                    if (Main.PaxJobsPresent && PaxJobsCompat.AllPaxStations().Contains(__instance.stationController))
                    {
                        PaxJobsCompat.OverrideSpawnFlagForPaxJ = true;
                        PaxJobsCompat.PaxJobsOrigGenJobsInStation(__instance.stationController.stationInfo.YardID);
                    }

                    StationIdCarSpawningPersistence.Instance.SetHasStationSpawnedCarsFlag(__instance.stationController, true);

                    __instance.StopJobGeneration();
                    ___generationCoro = __instance.StartCoroutine(CarSpawningJobGenerator.GenerateProceduralJobsCoroutine(__instance, ___generationRuleset));
                }
            } catch (Exception e) {
                Main.HandleUnhandledException(e, nameof(StationProceduralJobsController_Patch) + "." + nameof(TryToGenerateJobs_Prefix));
            }

            return false;
        }
    }
}