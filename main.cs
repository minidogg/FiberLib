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
using System.Drawing;


namespace FiberLib
{
    public class metadata
    {
        public static byte[] signature = [223, 145];
    }
    [BepInPlugin(pluginGuid, "FiberLib", "1.0.0")]
    [BepInProcess("BoplBattle.exe")]
    public class FiberLibPlugin : BaseUnityPlugin
    {
        public const string pluginGuid = "com.unluckycrafter.fiberlib";
        public static SteamManager curManager;
        public static SteamSocket curSocket;


        private void Awake()
        {

            // Plugin startup logic
            Logger.LogInfo($"FiberLib is loaded!");//feel free to remove this
            Harmony harmony = new Harmony(pluginGuid);

            //fetch manager
            MethodInfo original3 = AccessTools.Method(typeof(SteamManager), "Update");
            MethodInfo patch3 = AccessTools.Method(typeof(FiberLibPlugin), "FetchManager");
            harmony.Patch(original3, new HarmonyMethod(patch3));

            //fetch socket
            /*            MethodInfo original5 = AccessTools.Method(typeof(SteamSocket), "Update");
                        MethodInfo patch5 = AccessTools.Method(typeof(Plugin), "FetchSocket");
                        harmony.Patch(original5, new HarmonyMethod(patch5));*/

            MethodInfo original4 = AccessTools.Method(typeof(SteamSocket), "OnMessage");
            MethodInfo patch4 = AccessTools.Method(typeof(FiberLibPlugin), "OnMessageInterceptor");
            harmony.Patch(original4, new HarmonyMethod(patch4));
        }

        public static bool FetchManager(ref SteamManager ___instance)
        {
            curManager = ___instance;
            return true;
        }

        public static byte[] messageBuffer = new byte[1];

        public static bool OnMessageInterceptor(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            //setup for receiving packets
            messageBuffer = new byte[size];
            Marshal.Copy(data, messageBuffer, 0, size);
            if(!(messageBuffer[0] == metadata.signature[0] && messageBuffer[1] == metadata.signature[1]))return true;

            //handling packets
            Console.WriteLine("Custom packet received!");
            


            //return to default method
            return true;
        }

        private void OnGUI()
        {
            if (GUI.Button(new Rect(0, 50, 170f, 30f), "Send Packet"))
            {
                Console.WriteLine("Sending packet!");
                byte[] data = PacketCreator.constructPacket((byte[])[16,23], (byte[])[12]);
                PacketCreator.distributePacket(curManager,data);
            }
        }

    }
    public class PacketCreator()
    {
        public static byte[] Combine(byte[] first, byte[] second, byte[] third)
        {
            byte[] ret = new byte[first.Length + second.Length + third.Length];
            Buffer.BlockCopy(first, 0, ret, 0, first.Length);
            Buffer.BlockCopy(second, 0, ret, first.Length, second.Length);
            Buffer.BlockCopy(third, 0, ret, first.Length + second.Length,
                             third.Length);
            return ret;
        }
        public static byte[] constructPacket(byte[] pluginSignature, byte[] sendData)
        {
            int size = sendData.Length;
            byte[] data;
            data = Combine(metadata.signature,pluginSignature,sendData);

            return data;
        }
        public static bool distributePacket(SteamManager manager, byte[] packet, SendType sendType = SendType.Reliable)
        {
            for(int i = 0;i< manager.connectedPlayers.Count; i++)
            {
                manager.connectedPlayers[i].Connection.SendMessage(packet,sendType);
            }
            return true;
        }
    }
}
