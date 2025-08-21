using UnityEngine;
using System.Reflection;
using Steamworks;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Photon.Pun;

namespace peakReconnect
{
    [BepInPlugin("peakReconnect", "peakReconnect", "1.0.0")]
    public class peakReconnect : BaseUnityPlugin
    {
        private static ConfigEntry<string> lastServerConfig;
        private static ConfigEntry<Key> keyboardConfig;
        private static Key keyboardBind;
        private static ConfigEntry<GamepadButton> controllerConfig;
        private static GamepadButton controllerBind;
        private static CSteamID lastServer;
        public static peakReconnect Instance { get; private set; }

        private void Awake()
        {
            Instance = this;

            keyboardConfig = Config.Bind("Keybinds", "keyboardKey", Key.K, "keyboard bind to reconnect");
            keyboardBind = keyboardConfig.Value;

            controllerConfig = Config.Bind("Keybinds", "controllerButton", GamepadButton.LeftTrigger, "controller bind to reconnect");
            controllerBind = controllerConfig.Value;

            lastServerConfig = Config.Bind("General", "lastServer", "", "steam join code of the last lobby you were in");

            if (!string.IsNullOrEmpty(lastServerConfig.Value) && ulong.TryParse(lastServerConfig.Value, out ulong parsedID))
            {
                lastServer = new CSteamID(parsedID);
                Logger.LogInfo("Last server loaded: " + lastServer.ToString());
            }
            else
            {
                lastServer = CSteamID.Nil;
                Logger.LogInfo("No last server found, defaulting to Nil");
            }

            GameObject callbackObj = new GameObject("peakReconnect");
            callbackObj.AddComponent<onConnect>();
            DontDestroyOnLoad(callbackObj);
        }

        private static void JoinLobby()
        {
            if (lastServer == CSteamID.Nil)
            {
                Instance.Logger.LogInfo("Last server is Nil, halting join attempt");
                return;
            }

            Instance.Logger.LogInfo("attempting to join steam ID: " + lastServer.ToString());

            var handler = GameHandler.GetService<SteamLobbyHandler>();
            if (handler == null)
            {
                Instance.Logger.LogInfo("SteamLobbyHandler not found.");
                return;
            }

            var method = typeof(SteamLobbyHandler).GetMethod("JoinLobby", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                Instance.Logger.LogInfo("JoinLobby method not found.");
                return;
            }

            method.Invoke(handler, new object[] { lastServer });
        }

        private void Update()
        {
            if ((
                (Keyboard.current != null && Keyboard.current[keyboardBind].wasPressedThisFrame) ||
                (Gamepad.current != null && Gamepad.current[controllerBind].wasPressedThisFrame)
                ) && !GameHandler.GetService<SteamLobbyHandler>().InSteamLobby())
            {
                JoinLobby();
            }
        }

        public void UpdateConfig(CSteamID steamID)
        {
            lastServer = steamID;
            lastServerConfig.Value = steamID.m_SteamID.ToString();
            Config.Save();
        }
    }

    public class onConnect : MonoBehaviourPunCallbacks
    {
        public override void OnJoinedRoom()
        {
            if (GameHandler.GetService<SteamLobbyHandler>().InSteamLobby(out CSteamID lobbyID))
            {
                peakReconnect.Instance.UpdateConfig(lobbyID);
            }
            base.OnJoinedRoom();
        }
    }
}
