using BepInEx;
using System;
using HarmonyLib;
using JetBrains.Annotations;
using System.Reflection;
using BoplFixedMath;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using BepInEx.Logging;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Steamworks.Data;
using System.Runtime.InteropServices;


namespace BoplBattleTemplate
{
    [BepInPlugin(pluginGuid, "FiberLib", "1.0.0")]
    [BepInProcess("BoplBattle.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string pluginGuid = "com.unluckycrafter.fiberlib";
        public static SteamManager curManager;
        public static SteamSocket curSocket;


        private void Awake()
        {

            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_NAME} is loaded!");//feel free to remove this
            Harmony harmony = new Harmony(pluginGuid);

            //fetch manager
            MethodInfo original3 = AccessTools.Method(typeof(SteamManager), "Update");
            MethodInfo patch3 = AccessTools.Method(typeof(Plugin), "FetchManager");
            harmony.Patch(original3, new HarmonyMethod(patch3));

            //fetch socket
            /*            MethodInfo original5 = AccessTools.Method(typeof(SteamSocket), "Update");
                        MethodInfo patch5 = AccessTools.Method(typeof(Plugin), "FetchSocket");
                        harmony.Patch(original5, new HarmonyMethod(patch5));*/

            MethodInfo original4 = AccessTools.Method(typeof(SteamSocket), "OnMessage");
            MethodInfo patch4 = AccessTools.Method(typeof(Plugin), "OnMessageInterceptor");
            harmony.Patch(original4, new HarmonyMethod(patch4));
        }

        public static bool FetchManager(ref SteamManager ___instance, ref SteamManager ___fields)
        {
            curManager = ___instance;
            return true;
        }
        public static bool FetchSocket(ref SteamSocket ___instance)
        {
            curSocket = ___instance;
            return true;
        }
        public static void sendPacket()
        {

        }

        public static byte[] messageBuffer = new byte[2048];

        public static bool OnMessageInterceptor(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            //setup for receiving packets
            messageBuffer = new byte[2048];
            Marshal.Copy(data, messageBuffer, 0, size);
            Enum packetEnum = NetworkTools.ReadUICom(messageBuffer).PacketTypeAsEnum;

            //handling packets
            Console.WriteLine("Received a packet.");
    

            //return to default method
            return true;
        }

        private void OnGUI()
        {
            if (GUI.Button(new Rect(0, 50, 170f, 30f), "Send Packet"))
            {
                Console.WriteLine("Sending packet!");

            }
        }

    }
}
