using GameNetcodeStuff;
using HarmonyLib;
using LethalBots.AI;
using LethalBots.Managers;
using LethalBots.Utils.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace LethalBots.Patches.EnemiesPatches
{
    [HarmonyPatch(typeof(CadaverGrowthAI))]
    public class CadaverGrowthAIPatch
    {
        // TODO: Use delegates instead of using reflection to call the private methods
        private static MethodInfo displayFeverStatusEffectMethod = AccessTools.Method(typeof(CadaverGrowthAI), "DisplayFeverStatusEffect");
        private static MethodInfo increaseBackFlowersMethod = AccessTools.Method(typeof(CadaverGrowthAI), "IncreaseBackFlowers");

        /// <summary>
        /// Simple patch that makes sure that the movement hindering effect of the infection is removed for bots
        /// just like how it is for other players when the AI is disabled.
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPatch("OnDisable")]
        [HarmonyPostfix]
        public static void OnDisable_Postfix(CadaverGrowthAI __instance)
        {
            PlayerControllerB localPlayerController = GameNetworkManager.Instance.localPlayerController;
            for (int i = 0; i < __instance.playerInfections.Length; i++)
            {
                var playerInfection = __instance.playerInfections[i];
                PlayerControllerB playerController = StartOfRound.Instance.allPlayerScripts[i];
                if (playerController != null && playerController != localPlayerController && playerInfection.hinderingPlayerMovement)
                {
                    playerController.isMovementHindered--;
                    if (playerController.overridePoisonValue)
                    {
                        playerController.overridePoisonValue = false;
                    }
                    playerInfection.hinderingPlayerMovement = false;
                }
            }
        }

        /// <summary>
        /// Makes this function work for bots as well. I have no idea why Zeekerss decided to call GameNetworkManager.Instance for the local player controller instead of using the one that is already cached in a local variable, 
        /// but this patch replaces that call with the local variable.<br/>
        /// This is required for bots since GameNetworkManager.Instance.localPlayerController will be the local player, while the local variable will hold the correct player controller reference. 
        /// If this patch isn't applied, bots will trigger this as the local player in this function when they die.
        /// </summary>
        /// <remarks>
        /// TODO: Apply this to the OnLocalPlayerTalk function as well.
        /// </remarks>
        /// <param name="instructions"></param>
        /// <param name="generator"></param>
        /// <returns></returns>
        [HarmonyPatch("OnLocalPlayerDie")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> OnLocalPlayerDie_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var startIndex = -1;
            var codes = new List<CodeInstruction>(instructions);

            // Target property: GameNetworkManager.Instance.localPlayerController
            MethodInfo getGameNetworkManagerInstance = AccessTools.PropertyGetter(typeof(GameNetworkManager), "Instance");
            FieldInfo localPlayerControllerField = AccessTools.Field(typeof(GameNetworkManager), "localPlayerController");

            // ----------------------------------------------------------------------
            for (var i = 0; i < codes.Count - 1; i++)
            {
                if (codes[i].Calls(getGameNetworkManagerInstance)
                    && codes[i + 1].LoadsField(localPlayerControllerField))
                {
                    startIndex = i;
                    break;
                }
            }
            if (startIndex > -1)
            {
                // Replace with the local variable that holds the player controller,
                // for some reason Zeekerss decided to call GameNetworkManager.Instance for this one function call.
                codes[startIndex].opcode = OpCodes.Nop;
                codes[startIndex].operand = null;
                codes[startIndex + 1].opcode = OpCodes.Ldarg_1;
                codes[startIndex + 1].operand = null;
                startIndex = -1;
            }
            else
            {
                Plugin.LogError($"LethalBot.Patches.EnemiesPatches.OnLocalPlayerDie_Transpiler could not replace localPlayerController with locally cached player");
            }

            return codes.AsEnumerable();
        }

        /// <summary>
        /// Simple patch to have the bot also mimic the local player logic.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="loudness"></param>
        /// <param name="playerId"></param>
        [HarmonyPatch("CoughSporesRpc")]
        [HarmonyPostfix]
        public static void CoughSporesRpc_Postfix(CadaverGrowthAI __instance, float loudness, int playerId)
        {
            // Mimic the local player logic for bots as well.
            PlayerControllerB sickPlayer = StartOfRound.Instance.allPlayerScripts[playerId];
            // NEEDTOVALIDATE: I'm pretty sure the actual function already does this.......
            //PlayerInfection obj = __instance.playerInfections[playerId];
            //obj.faceSpores.SetFloat("BurstAmount", Mathf.Lerp(620f, 1200f, loudness));
            //obj.faceSporesOutput = 0.8f;
            //obj.faceSpores.transform.position = sickPlayer.bodyParts[0].position + sickPlayer.bodyParts[0].forward * 0.15f;
            //obj.faceSpores.transform.rotation = sickPlayer.bodyParts[0].transform.rotation;
            //obj.faceSpores.Play();

            LethalBotAI[] lethalBotAIs = LethalBotManager.Instance.GetLethalBotsAIOwnedByLocal();
            foreach (var lethalBotAI in lethalBotAIs)
            {
                PlayerControllerB? lethalBotController = lethalBotAI?.NpcController?.Npc;
                if (lethalBotController != null)
                {
                    // Must be looking at the player in order to potentially infect them.
                    if (!(sickPlayer.LineOfSightToPositionAngle(lethalBotController.gameplayCamera.transform.position, 12, 0f) < 30f))
                    {
                        continue;
                    }

                    float num = Vector3.Distance(lethalBotController.transform.position, sickPlayer.transform.position);
                    //if (num < 6f)
                    //{
                    //    HUDManager.Instance.DisplayStatusEffect("HEALTH RISK!\n\nAir filter overwhelmed by particulates");
                    //}
                    if ((float)UnityEngine.Random.Range(0, 1000) < Mathf.Lerp(50f, 1f, Mathf.Clamp(num * num / 60f, 0f, 1f)))
                    {
                        bool severe = false;
                        if (UnityEngine.Random.Range(0, 100) < 70)
                        {
                            severe = true;
                        }
                        __instance.InfectPlayer(lethalBotController, severe);
                        __instance.InfectPlayerRpc((int)lethalBotController.playerClientId, severe);
                    }
                }
            }
        }

        // FIXME: The game uses StartOfRound.Instance.occlusionCuller.currentTile to find what tiles are active for the infection checks
        // but the bot's don't have an occlusion culler, so I can't check if they are on an active tile or not.
        // I need to see how the base game handles this for the local player, and mimic that logic for the bots as well.
        // UPDATE: After looking into it, it looks like the occlusionCuller.currentTile check is done as an optimization to avoid checking tiles the local player isn't on.
        // I could just check the entire list of GrowthTiles, but that would be pretty bad for performance. For now, bots can't get infected this way, for now.
        // I'm going to look into how  GrowthTile finds what tiles its on as I could use that logic to find what tile the bot is on and check if it's active or not.
        //[HarmonyPatch("InfectPlayers")]
        //[HarmonyPostfix]
        //public static void InfectPlayers_Postfix(CadaverGrowthAI __instance)
        //{
        //    // Mimic the local player logic for bots as well.
        //    LethalBotAI[] lethalBotAIs = LethalBotManager.Instance.GetLethalBotsAIOwnedByLocal();
        //    foreach (var lethalBotAI in lethalBotAIs)
        //    {
        //        PlayerControllerB? lethalBotController = lethalBotAI?.NpcController?.Npc;
        //        if (lethalBotController != null 
        //            && lethalBotController.isPlayerControlled 
        //            && !lethalBotController.isPlayerDead)
        //        {
        //            __instance.InfectPlayer(lethalBotController, false);
        //        }
        //    }
        //}

        /// <summary>
        /// Since <see cref="BurstFromPlayer_Prefix(CadaverGrowthAI, PlayerControllerB, Vector3, Vector3)"/> runs before the base game logic, 
        /// we need to wait until the base game logic has done its thing before we can mimic it for the bots, 
        /// otherwise we risk breaking the flow of the base game's logic and potentially causing bugs.
        /// </summary>
        /// <param name="cadaver"></param>
        /// <param name="player"></param>
        /// <returns></returns>
        private static IEnumerator BurstFromPlayerCoroutine(CadaverGrowthAI cadaver, PlayerControllerB player, int infectedNum)
        {
            // Wait to the end frames to make sure the base game logic has done its thing before we try to mimic it for the bots.
            PlayerInfection playerInfection = cadaver.playerInfections[player.playerClientId];
            yield return null;
            yield return new WaitForEndOfFrame();
            if (player != null && player.isPlayerControlled && playerInfection.infected && playerInfection.infectionMeter > 0.85f && Vector3.Distance(player.transform.position, cadaver.bloomEnemies[infectedNum].transform.position) < 14f)
            {
                cadaver.BurstFromPlayer(player, player.transform.position, player.transform.eulerAngles);
                cadaver.SyncBurstFromPlayerRpc((int)player.playerClientId, player.transform.position, player.transform.eulerAngles);
            }
        }

        /// <summary>
        /// A simple prefix patch to make the burst from player logic work for bots as well. 
        /// Since the base game logic for bursting from the player is done in a way that it has to be done in a specific order, 
        /// we have to wait until the base game logic has done its thing before we can mimic it for the bots, 
        /// otherwise we risk breaking the flow of the base game's logic and potentially causing bugs.
        /// </summary>
        /// <remarks>
        /// This is a prefix and not a postfix since we need to get the index of the bloom enemy that will burst from the player before the base game logic does.
        /// </remarks>
        /// <param name="__instance"></param>
        /// <param name="playerScript"></param>
        /// <param name="burstPosition"></param>
        /// <param name="burstRotation"></param>
        [HarmonyPatch("BurstFromPlayer")]
        [HarmonyPrefix]
        public static void BurstFromPlayer_Prefix(CadaverGrowthAI __instance, PlayerControllerB playerScript, Vector3 burstPosition, Vector3 burstRotation)
        {
            // Make sure we do the same checks that normally happen, just in case the function returned early for some reason.
            int num = -1;
            Vector3 position = playerScript.transform.position;
            for (int i = 0; i < __instance.bloomEnemies.Length; i++)
            {
                if (__instance.bloomEnemies[i] == null)
                {
                    Plugin.LogError($"Cadaver growth AI: Tried to burst from player, but there are no bloom enemies on standby? index: {i}");
                    return;
                }
                if (!__instance.bloomEnemies[i].hasBurst)
                {
                    // NOTE: DON'T DO THIS, the base game will do this. If we do this now, we risk breaking the flow of the base game's logic.
                    //__instance.bloomEnemies[i].BurstForth(playerScript, kill: true, burstPosition, burstRotation);
                    //if (__instance.playerInfections[playerScript.playerClientId].backFlowers != null)
                    //{
                    //    UnityEngine.Object.Destroy(__instance.playerInfections[playerScript.playerClientId].backFlowers);
                    //}
                    num = i;
                    break;
                }
            }
            if (num == -1)
            {
                Plugin.LogError("Cadaver growth AI: Tried to burst from player, but there are no bloom enemies on standby? B");
                return;
            }

            // Mimic the local player logic for bots as well.
            LethalBotAI[] lethalBotAIs = LethalBotManager.Instance.GetLethalBotsAIOwnedByLocal();
            foreach (var lethalBotAI in lethalBotAIs)
            {
                PlayerControllerB? lethalBotController = lethalBotAI?.NpcController?.Npc;
                if (lethalBotController != null)
                {
                    lethalBotAI?.StartCoroutine(BurstFromPlayerCoroutine(__instance, lethalBotController, num));
                }
            }
        }

        [HarmonyPatch("ProgressPlayerInfections")]
        [HarmonyPostfix]
        public static void ProgressPlayerInfections_Postfix(CadaverGrowthAI __instance, ref int ___numberOfInfected)
        {
            // Mimic the local player logic for bots as well.
            // NEEDTOVALIDATE: Depending on how often this function is called, which appears to be every frame, this might cause performance issues with a ton
            // of bots, I may have to limit how often this logic runs for bots, maybe every 0.5 seconds or something like that.
            // I just need to remeber to include the throttle time or they could get infected way slower than intended.
            for (int i = 0; i < __instance.playerInfections.Length; i++)
            {
                PlayerInfection playerInfection = __instance.playerInfections[i];
                if (!playerInfection.infected)
                {
                    continue;
                }

                PlayerControllerB playerController = StartOfRound.Instance.allPlayerScripts[i];
                if (!playerController.isPlayerControlled)
                {
                    continue; // Base game already handles this.
                }
                else
                {
                    // NOTE: LethalBotInfection handles networking information about the infection for bots and only the bot's owner can edit it.
                    LethalBotAI? lethalBotAI = LethalBotManager.Instance.GetLethalBotAIIfLocalIsOwner(playerController);
                    if (lethalBotAI == null)
                    {
                        continue;
                    }

                    LethalBotInfection lethalBotInfection = lethalBotAI.BotInfectionData.Value;
                    if (playerInfection.infectionMeter >= 1f)
                    {
                        if (playerInfection.burstMeter >= 1f)
                        {
                            playerInfection.infected = false;
                            ___numberOfInfected--;
                            __instance.BurstFromPlayer(playerController, playerController.transform.position, playerController.transform.eulerAngles);
                            __instance.SyncBurstFromPlayerRpc((int)playerController.playerClientId, playerController.transform.position, playerController.transform.eulerAngles);
                            continue;
                        }
                        playerController.poison = Mathf.Lerp(playerController.poison, 0f, Time.deltaTime * 0.3f);
                        if (playerInfection.burstMeter >= 0.9f)
                        {
                            if (!StartOfRound.Instance.shipIsLeaving)
                            {
                                if (StartOfRound.Instance.livingPlayers <= 1)
                                {
                                    //HUDManager.Instance.DisplayStatusEffect("HIGH FEVER DETECTED!!!\nFOREIGN BODIES DETECTED!!!\nIRREGULAR BRAINWAVE DETECTED!!!");
                                    playerInfection.burstMeter += Time.deltaTime * 0.0055f;
                                }
                                else
                                {
                                    playerInfection.burstMeter += Time.deltaTime * 0.068f;
                                }
                                // There could be a case where the bot changes ownership with the isMovementHindered check.
                                // It will be a rare case, but it could cause the bot to not get the movement hindered effect when it should, or to get the movement hindered effect when it shouldn't.
                                // I will leave it like this for now, but if it becomes a problem, I can add an RPC to sync the movement hindered state change.
                                if (StartOfRound.Instance.connectedPlayersAmount > 0 && !playerInfection.hinderingPlayerMovement)
                                {
                                    playerController.isMovementHindered++;
                                    playerInfection.hinderingPlayerMovement = true;
                                }
                                //HUDManager.Instance.cadaverFilter = Mathf.Lerp(0f, 1f, (playerInfection.burstMeter - 0.9f) / 0.1f);
                                //SoundManager.Instance.alternateEarsRinging = true;
                                //SoundManager.Instance.earsRingingTimer = 1f;
                            }
                            continue;
                        }
                        float num;
                        if (StartOfRound.Instance.connectedPlayersAmount > 0)
                        {
                            if (StartOfRound.Instance.livingPlayers == 1)
                            {
                                num = 0.75f;
                            }
                            else
                            {
                                float num2 = 2000f;
                                for (int j = 0; j < StartOfRound.Instance.allPlayerScripts.Length; j++)
                                {
                                    if (StartOfRound.Instance.allPlayerScripts[j] != playerController)
                                    {
                                        float num3 = Vector3.Distance(StartOfRound.Instance.allPlayerScripts[j].transform.position, playerController.transform.position);
                                        if (num3 < num2)
                                        {
                                            num2 = num3;
                                        }
                                    }
                                }
                                num = 1f;
                                num = Mathf.Lerp(3f, 0.015f, Mathf.Clamp(num2 / 30f, 0f, 1f));
                            }
                        }
                        else
                        {
                            num = 1.25f;
                        }
                        playerInfection.burstMeter += Time.deltaTime * num * __instance.BurstSpeedMultiplier;
                        continue;
                    }
                    if (playerInfection.healing > 0)
                    {
                        playerInfection.infectionMeter -= Time.deltaTime * 0.08f * (float)playerInfection.healing;
                        //HUDManager.Instance.DisplayStatusEffect("FIGHTING INFECTION!\nReduction in fever");
                        if (playerInfection.infectionMeter <= 0f)
                        {
                            playerInfection.infectionMeter = 0f;
                            __instance.CurePlayer(i);
                            __instance.CurePlayerRpc(i);
                        }
                        continue;
                    }
                    float num4 = 1f;
                    bool flag = false;
                    if (!playerController.isInsideFactory && TimeOfDay.Instance.normalizedTimeOfDay < 0.6f && !Physics.Linecast(TimeOfDay.Instance.sunDirect.transform.position, StartOfRound.Instance.allPlayerScripts[i].gameplayCamera.transform.position, StartOfRound.Instance.collidersRoomDefaultAndFoliage, QueryTriggerInteraction.Ignore))
                    {
                        flag = true;
                    }
                    if (flag)
                    {
                        num4 *= 1.15f;
                    }
                    if (playerController.NearOtherPlayers(15f))
                    {
                        num4 = ((!(playerInfection.infectionMeter > 0.925f)) ? (num4 * 0.7f) : (num4 * 1.5f));
                    }
                    num4 *= Mathf.Lerp(1f, 0.85f, (float)___numberOfInfected / (float)StartOfRound.Instance.livingPlayers);
                    num4 *= 1f + __instance.totalTimeSpentInPlants / 15f;
                    bool flag2 = StartOfRound.Instance.connectedPlayersAmount == 0;
                    float num5 = Time.deltaTime * __instance.InfectionSpeedMultiplier * num4 * playerInfection.multiplier;
                    if (flag2)
                    {
                        num5 *= 0.45f;
                    }
                    if (Time.realtimeSinceStartup - lethalBotInfection.timeAtLastHealing < 0.7f)
                    {
                        continue;
                    }
                    playerInfection.infectionMeter = Mathf.Clamp(playerInfection.infectionMeter + num5, 0f, 1f);
                    lethalBotInfection.showSignsMeter += num5;
                    if (playerInfection.infectionMeter > 0.55f && !flag2)
                    {
                        playerInfection.bloomOnDeath = true;
                    }
                    if (playerController.overridePoisonValue)
                    {
                        playerController.poison = Mathf.Lerp(playerController.poison, lethalBotInfection.setPoison, Time.deltaTime * 0.7f);
                    }
                    if (StartOfRound.Instance.connectedPlayersAmount == 0)
                    {
                        if (lethalBotInfection.showSignsMeter > 0.05f)
                        {
                            lethalBotInfection.showSignsMeter = 0f;
                            // FIXME: I don't feel like traspiling the DisplayFeverStatusEffect function to skip some of the logic for bots,
                            // so for now, we will recreate the logic here.
                            const float infectionThreshold = 0.8f;
                            float t = (playerInfection.infectionMeter - infectionThreshold) / (1f - infectionThreshold);
                            playerController.overridePoisonValue = true;
                            lethalBotInfection.setPoison = Mathf.Lerp(0f, 0.4f, t);
                            //displayFeverStatusEffectMethod.Invoke(__instance, new object[] { i, 0.8f } );
                        }
                    }
                    else if (lethalBotInfection.showSignsMeter > 0.05f)
                    {
                        lethalBotInfection.showSignsMeter = 0f;
                        float num6 = Mathf.Lerp(5f, 50f, Mathf.Clamp(playerInfection.infectionMeter / 0.9f, 0f, 1f));
                        if (UnityEngine.Random.Range(0f, 100f) < num6)
                        {
                            increaseBackFlowersMethod.Invoke(__instance, new object[] { i });
                            __instance.IncreaseBackFlowersRpc(i, playerInfection.infectionMeter);
                        }
                        else
                        {
                            __instance.SyncInfectionMeterRpc((int)playerController.playerClientId, playerInfection.infectionMeter);
                        }
                    }
                }
            }
        }
    }
}
