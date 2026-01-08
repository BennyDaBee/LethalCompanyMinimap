// ----------------------------------------------------------------------
// Copyright (c) Tyzeron. All Rights Reserved.
// Licensed under the GNU Affero General Public License, Version 3
// ----------------------------------------------------------------------

using GameNetcodeStuff;
using HarmonyLib;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LethalCompanyMinimap.Patches
{
    [HarmonyPatch(typeof(HUDManager))]
    internal class HUDManagerPatch
    {
        public static readonly string prefix = $"<color=#00ffffff>[{MinimapMod.modName}]</color> ";
        public static readonly string prefixForBroadcast = "Tyzeron.Minimap";
        private static readonly Regex parserRegex = new Regex(@"\A<size=0>" + Regex.Escape(prefixForBroadcast) + @"/([ -~]+)/([ -~]+)/([ -~]+)</size>\z", RegexOptions.Compiled);
        private static IDictionary<string, string> myBroadcasts = new Dictionary<string, string>();

        public static void SendClientMessage(string message)
        {
            HUDManager.Instance.AddTextToChatOnServer($"{prefix}{message}");
        }

        [HarmonyPatch("AddTextMessageServerRpc")]
        [HarmonyPrefix]
        static bool DontSendMinimapMessagesPatch(string chatMessage)
        {
            // Don't block broadcast messages - they need to go through
            if (chatMessage.Contains(prefixForBroadcast))
            {
                return true; // Allow the message through
            }
            
            if (chatMessage.StartsWith(prefix))
            {
                return false;
            }
            return true;
        }

        [HarmonyPatch("AddTextMessageServerRpc")]
        [HarmonyPostfix]
        static void ReceiveBroadcastMessagesPatch(string chatMessage)
        {
            // Process broadcast messages - this runs on server and should also process locally
            ProcessBroadcastMessage(chatMessage);
        }

        // Also patch the method that adds messages to chat on clients
        [HarmonyPatch("AddChatMessage")]
        [HarmonyPostfix]
        static void ReceiveBroadcastMessagesClientPatch(string chatMessage, string nameOfUserWhoTyped, int playerWhoSent, bool dontRepeat)
        {
            // Process broadcast messages on clients when chat messages are added
            ProcessBroadcastMessage(chatMessage);
        }

        private static void ProcessBroadcastMessage(string chatMessage)
        {
            if (string.IsNullOrEmpty(chatMessage) || MinimapMod.minimapGUI == null)
                return;

            // Parse broadcast messages
            Match match = parserRegex.Match(chatMessage);
            if (match.Success)
            {
                string clientId = match.Groups[1].Value;
                string signature = match.Groups[2].Value;
                string data = match.Groups[3].Value;

                // Handle host override settings
                if (signature == "HostOverrideSettings")
                {
                    MinimapMod.minimapGUI.ApplyHostSettings(data);
                }
                // Handle host override disabled
               else if (signature == "HostOverrideDisabled")
               {
                   MinimapMod.minimapGUI.RestoreFromConfig();
               }
            }
        }

        public static void SendMinimapBroadcast(string signature, string data = "null")
        {
            int clientId = (int)GameNetworkManager.Instance.localPlayerController.playerClientId;
            if (!myBroadcasts.ContainsKey(signature))
            {
                myBroadcasts.Add(signature, data);
            }
            else
            {
                myBroadcasts[signature] = data;
            }
            HUDManager.Instance.AddTextToChatOnServer($"<size=0>{prefixForBroadcast}/{clientId}/{signature}/{data}</size>");
        }

    }
}
