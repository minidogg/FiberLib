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
using System.Linq;
using BepInEx.Configuration;
using Steamworks;
using System.Security.Principal;
using System.Net.Sockets;


namespace FiberLib
{
    public class metadata
    {
        public static byte[] signature = [223, 145];
    }

    [BepInPlugin(pluginGuid, "FiberLib", "1.2.0")]
    [BepInProcess("BoplBattle.exe")]
    public class FiberLibPlugin : BaseUnityPlugin
    {
        public const string pluginGuid = "com.FiberDevs.FiberLib";
        internal static SteamManager curManager;
        //internal static SteamSocket curSocket;

        private Signature testSignature;


        private void handler(Packet packet, Connection connection, NetIdentity identity)
        {
            Console.WriteLine("Plugin received packet");
            byte[] data = packet.data;
            Console.WriteLine(Encoding.Default.GetString(data));
            Console.WriteLine(packet.player.Id);
            return;
        }

        private void Awake()
        {
            testSignature = PacketManager.RegisterPacketReciveHandler(handler);
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
            // Setup for receiving packets
            byte[] messageBuffer = new byte[size];
            Marshal.Copy(data, messageBuffer, 0, size);

            if (!identity.SteamId.IsValid)
            {
                Debug.Log("got message from invalid steamId");
                return;
            }
            SteamId steamId = identity.SteamId;
            Friend friend = new Friend(steamId);
            if (!friend.IsIn(SteamManager.instance.currentLobby.Id))
            {
                Debug.Log("(FiberLib) Ignored a msg from " + identity.SteamId.ToString());
                return;
            }

            if (messageBuffer[0] != metadata.signature[0] || messageBuffer[1] != metadata.signature[1]) return;

            //handling packets
            PacketManager.RunHandler(messageBuffer, connection, identity);
        }

        private void OnGUI()
        {
            if (GUI.Button(new Rect(0, 50, 170f, 30f), "Send Packet"))
            {
                Console.WriteLine("Sending packet!");
                PacketManager.SendPacket(new Packet(testSignature, Encoding.UTF8.GetBytes("Hello, World!")));
            }
        }
    }

    public struct Signature
    {
        internal ushort sign;

        public Signature() => throw new UnauthorizedAccessException("Signature cannot be created");

        internal Signature(ushort sign)
        {
            this.sign = sign;
        }
    }

    public struct Packet(Signature signature, byte[] data, bool useNetIdentity = false, NetIdentity identity = new NetIdentity())
    {
        public Signature signature = signature;
        public byte[] data = data;
        public Player player = useNetIdentity == true ? PlayerHandler.Get().PlayerList().Find(x => x.steamId == identity.SteamId) : new Player();
    }

    public class PacketUtils()
    {
        internal static byte[] GetDataFromSteamPacket(byte[] byteArray)
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
        public delegate void PacketReciveHandler(Packet packet, Connection connection, NetIdentity identity);
        private static readonly Dictionary<Signature, PacketReciveHandler> registeredMethods = [];


        private static byte[] Combine(byte[] first, byte[] second, byte[] third)
        {
            byte[] ret = new byte[first.Length + second.Length + third.Length];
            Buffer.BlockCopy(first, 0, ret, 0, first.Length);
            Buffer.BlockCopy(second, 0, ret, first.Length, second.Length);
            Buffer.BlockCopy(third, 0, ret, first.Length + second.Length, third.Length);
            return ret;
        }

        private static byte[] ConstructPacket(byte[] pluginSignature, byte[] sendData)
        {
            // Todo: discovered that it is more efficient to use a list and then convert it to an array
            int size = sendData.Length;
            byte[] data;
            data = Combine(metadata.signature, pluginSignature, sendData);

            return data;
        }

        private static bool DistributePacket(SteamManager manager, byte[] packet, SendType sendType = SendType.Reliable)
        {
            foreach (SteamConnection player in manager.connectedPlayers)
            {
                player.Connection.SendMessage(packet, sendType);
            }
            return true;
        }

        private static int sign = -1;
        public static Signature RegisterPacketReciveHandler(PacketReciveHandler handler)
        {
            if (handler == null)
                throw new ArgumentNullException("handler was null");

            // Max 65536 plugin can be registered
            if (sign >= ushort.MaxValue)
                throw new OverflowException("Can't register plugin because there is too much registered plugin");

            sign++;

            Signature signature = new((ushort)sign);
            registeredMethods.Add(signature, handler);
            return signature;
        }

        public static void SendPacket(Packet packet)
        {
            byte[] data = ConstructPacket(PacketUtils.SplitUShort(packet.signature.sign), packet.data);
            DistributePacket(FiberLibPlugin.curManager, data);
        }

        internal static void RunHandler(byte[] packet, Connection connection, NetIdentity identity)
        {
            Signature sign = new(PacketUtils.MakeUShort(packet[2], packet[3]));
            try
            {
                byte[] data = PacketUtils.GetDataFromSteamPacket(packet);
                // Structs are hashed by value so making new with same values is the same struct
                registeredMethods[sign](new Packet(sign, data, true, identity), connection, identity);
            }
            catch (ArgumentOutOfRangeException) { }
        }
    }
}
