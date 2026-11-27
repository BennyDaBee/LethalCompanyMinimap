// ----------------------------------------------------------------------
// Copyright (c) Tyzeron. All Rights Reserved.
// Licensed under the GNU Affero General Public License, Version 3
// ----------------------------------------------------------------------

using HarmonyLib;
using Unity.Collections;
using Unity.Netcode;

namespace LethalCompanyMinimap.Patches
{
    internal class HUDManagerPatch
    {
        //Declare Strings for clients requesting state, and host sending state.
        const string RequestMessage = "LethalCompanyMinimap_HostOverrideRequest";
        const string StateMessage = "LethalCompanyMinimap_HostOverrideState";
        static bool registered;

        //Sends host Overrise Settings or If Disabled via Custom Message Manager
        public static void SendMinimapBroadcast(string signature, string data = "null", ulong? clientId = null)
        {
            var network = NetworkManager.Singleton;
            if (network == null || network.CustomMessagingManager == null)
                return;

            string payload = signature + "\n" + data;
            using (var writer = new FastBufferWriter(FastBufferWriter.GetWriteSize(payload), Allocator.Temp))
            {
                writer.WriteValueSafe(payload);
                //If Client ID is present, will send to that one client, otherwise will send to all players. Used Reliable to ensure delivery if message is dropped
                if (clientId.HasValue)
                    network.CustomMessagingManager.SendNamedMessage(StateMessage, clientId.Value, writer, NetworkDelivery.Reliable);
                else
                    network.CustomMessagingManager.SendNamedMessageToAll(StateMessage, writer, NetworkDelivery.Reliable);
            }
        }
        //When a player joins (Host or Player), they subscribe to OnRequest and OnState. A player will send RequestMessage to ServerClientID (Host).
        public static void RegisterAndRequest()
        {
            var network = NetworkManager.Singleton;
            if (network == null || network.CustomMessagingManager == null)
                return;
            //Subscribes and marks registered
            if (!registered)
            {
                network.CustomMessagingManager.RegisterNamedMessageHandler(RequestMessage, OnRequest);
                network.CustomMessagingManager.RegisterNamedMessageHandler(StateMessage, OnState);
                registered = true;
            }

            if (StartOfRound.Instance != null && StartOfRound.Instance.IsHost)
                return;
            //Builds and Sends message
            using (var writer = new FastBufferWriter(sizeof(int), Allocator.Temp))
            {
                writer.WriteValueSafe(1);
                network.CustomMessagingManager.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.Reliable);
            }
        }
        //Will clear the register so next session you create can subscribe again. Restore From Config will enable the players original settings that they had prior to joining
        public static void OnDisconnect()
        {
            registered = false;
            if (MinimapMod.minimapGUI != null)
                MinimapMod.minimapGUI.RestoreFromConfig();
        }
        //When host receives, will send the senderID the current state string. If HO is enabled, it will send settings, otherwise just state HO is disabled
        static void OnRequest(ulong senderId, FastBufferReader reader)
        {
            if (StartOfRound.Instance == null || !StartOfRound.Instance.IsHost || MinimapMod.minimapGUI == null)
                return;
            //Sends the senderID the current settings or is HO is disabled
            if (MinimapMod.minimapGUI.hostOverride)
                SendMinimapBroadcast("HostOverrideSettings", MinimapMod.minimapGUI.SerializeToggleSettings(), senderId);
            else
                SendMinimapBroadcast("HostOverrideDisabled", "restore", senderId);
        }
        //When client receives settings from host, it will apply the settings, otherwise ensure that their original settings get renabled
        static void OnState(ulong senderId, FastBufferReader reader)
        {
            if (NetworkManager.Singleton == null || senderId != NetworkManager.ServerClientId)
                return;
            if (StartOfRound.Instance != null && StartOfRound.Instance.IsHost)
                return;
            if (MinimapMod.minimapGUI == null)
                return;

            reader.ReadValueSafe(out string payload);
            int split = payload.IndexOf('\n');
            if (split < 0)
                return;
            //Applies the settings, otherwise ensures client settings are restored if host disables the Override
            string signature = payload.Substring(0, split);
            string data = payload.Substring(split + 1);
            if (signature == "HostOverrideSettings")
                MinimapMod.minimapGUI.ApplyHostSettings(data);
            else if (signature == "HostOverrideDisabled")
                MinimapMod.minimapGUI.RestoreFromConfig();
        }
    }
    [HarmonyPatch(typeof(GameNetworkManager))]
        //Ensures that on client disconnect the disconnect process runs to unlock the client settings and restore their original settings. 
        internal class GameNetworkManagerPatch
        {
            [HarmonyPatch("StartDisconnect")]
            [HarmonyPostfix]
            static void StartDisconnectPatch()
            {
                HUDManagerPatch.OnDisconnect();
            }
        }
}
