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
using System.Collections;
using System.Text;


namespace FiberLib
{
    public class metadata
    {
        public static byte[] signature = [223, 145];
    }

    [BepInPlugin(pluginGuid, "FiberLib", "1.1.0")]
    [BepInProcess("BoplBattle.exe")]
    public class FiberLibPlugin : BaseUnityPlugin
    {
        public const string pluginGuid = "com.unluckycrafter.fiberlib";
        internal static SteamManager curManager;
        //internal static SteamSocket curSocket;

		private ushort testSignature;

        private void handler(byte[] packet, Connection connection, NetIdentity identity)
        {
            Console.WriteLine("Plugin received packet");
            byte[] data = PacketUtils.GetData(packet);
            Console.WriteLine(Encoding.Default.GetString(data));
            return;
        }

        private void Awake()
        {
            /*testSignature = PacketManager.RegisterPacketReciveHandler(handler);*/
            // Plugin startup logic
            Logger.LogInfo($"FiberLib is loaded!");
            Harmony harmony = new Harmony(pluginGuid);

            //fetch manager
            MethodInfo original3 = AccessTools.Method(typeof(SteamManager), "Update");
            MethodInfo patch3 = AccessTools.Method(typeof(FiberLibPlugin), nameof(FetchManager));
            harmony.Patch(original3, new HarmonyMethod(patch3));

            //fetch socket
            /*            MethodInfo original5 = AccessTools.Method(typeof(SteamSocket), "Update");
                        MethodInfo patch5 = AccessTools.Method(typeof(Plugin), "FetchSocket");
                        harmony.Patch(original5, new HarmonyMethod(patch5));*/

            MethodInfo original4 = AccessTools.Method(typeof(SteamSocket), "OnMessage");
            MethodInfo patch4 = AccessTools.Method(typeof(FiberLibPlugin), nameof(OnMessageInterceptor));
            harmony.Patch(original4, new HarmonyMethod(patch4));
        }

        private static void FetchManager(ref SteamManager ___instance)
        {
            curManager = ___instance;
        }

		private static void OnMessageInterceptor(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            //setup for receiving packets
            byte[] messageBuffer = new byte[size];
            Marshal.Copy(data, messageBuffer, 0, size);
            if (messageBuffer[0] != metadata.signature[0] || messageBuffer[1] != metadata.signature[1]) return;

            //handling packets
            PacketManager.RunHandler(messageBuffer,  connection,  identity);
        }

/*        private void OnGUI()
        {
            if (GUI.Button(new Rect(0, 50, 170f, 30f), "Send Packet"))
            {
                Console.WriteLine("Sending packet!");
                PacketUtils.SendPacket(testSignature, Encoding.UTF8.GetBytes("Hello, World!"));
            }
        }*/
    }

    public class PacketUtils()
    {
        public static void SendPacket(ushort signature, byte[] sendData)
        {
            byte[] data = PacketManager.ConstructPacket(SplitUShort(signature), sendData);
            PacketManager.DistributePacket(FiberLibPlugin.curManager, data);
        }

        public static byte[] GetData(byte[] byteArray)
        {
            byte[] result = new byte[byteArray.Length - 4];

            Array.Copy(byteArray, 4, result, 0, result.Length);

            return result;
        }

        public static byte[] SplitUShort(ushort number) => [(byte)(number >> 8), (byte)number];
        public static ushort MakeUShort(byte byte1, byte byte2) => (ushort)((byte1 << 8) + byte2);
    }

    public class PacketManager()
    {
        public delegate void PacketReciveHandler(byte[] data, Connection connection, NetIdentity identity);

        private static readonly List<PacketReciveHandler> registeredMethods = [];

        private static byte[] Combine(byte[] first, byte[] second, byte[] third)
        {
            byte[] ret = new byte[first.Length + second.Length + third.Length];
            Buffer.BlockCopy(first, 0, ret, 0, first.Length);
            Buffer.BlockCopy(second, 0, ret, first.Length, second.Length);
            Buffer.BlockCopy(third, 0, ret, first.Length + second.Length, third.Length);
            return ret;
        }

        internal static byte[] ConstructPacket(byte[] pluginSignature, byte[] sendData)
        {
            // Todo: discovered that it is more efficient to use a list and then convert it to an array
            int size = sendData.Length;
            byte[] data;
            data = Combine(metadata.signature, pluginSignature, sendData);

            return data;
        }

        internal static bool DistributePacket(SteamManager manager, byte[] packet, SendType sendType = SendType.Reliable)
        {
            foreach (SteamConnection player in manager.connectedPlayers)
            {
                player.Connection.SendMessage(packet, sendType);
            }
            return true;
        }

        private static int sign = -1;
        public static ushort RegisterPacketReciveHandler(PacketReciveHandler handler)
        {
            if(handler == null)
                throw new ArgumentNullException("handler was null");

            // Max 65536 plugin can be registered
            if (sign >= ushort.MaxValue)
                throw new OverflowException("Can't register plugin because there is too much registered plugin");

            sign++;

            registeredMethods.Add(handler);
            return (ushort)sign;
        }

        public static void RunHandler(byte[] packet, Connection connection, NetIdentity identity)
        {
            ushort sign = PacketUtils.MakeUShort(packet[2], packet[3]);
	    try
	    {
                registeredMethods[sign](packet, connection, identity);
	    }
	    catch (ArgumentOutOfRangeException) {}
        }
    }
}
