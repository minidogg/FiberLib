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
    [BepInPlugin(pluginGuid, "FiberLib", "1.0.0")]
    [BepInProcess("BoplBattle.exe")]
    public class FiberLibPlugin : BaseUnityPlugin
    {
        public const string pluginGuid = "com.unluckycrafter.fiberlib";
        public static SteamManager curManager;
        public static SteamSocket curSocket;

        private void handler(byte[] packet)
        {
            Console.WriteLine("Plugin received packet");
            void loop(byte theByte)
            {
                Console.WriteLine(theByte);
            }
            packet.Do(loop);
            return;
        }
        public byte[] testSignature;

        private void Awake()
        {
            testSignature = PacketManager.RegisterPlugin(handler);
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
            if (!(messageBuffer[0] == metadata.signature[0] && messageBuffer[1] == metadata.signature[1])) return true;

            //handling packets
            PacketManager.RunHandler(messageBuffer);



            //return to default method
            return true;
        }

        private void OnGUI()
        {
            if (GUI.Button(new Rect(0, 50, 170f, 30f), "Send Packet"))
            {
                Console.WriteLine("Sending packet!");
                PacketUtils.SendPacket(testSignature, Encoding.UTF8.GetBytes("Hello, World!"));
            }
        }

    }
    public class PacketUtils()
    {
        public static void SendPacket(byte[] signature, byte[] sendData)
        {
            byte[] data = PacketManager.constructPacket(signature, sendData);
            PacketManager.distributePacket(FiberLibPlugin.curManager, data);
        }
        public static byte[] GetData(byte[] byteArray)
        {
            byte[] result = new byte[byteArray.Length - 4];

            Array.Copy(byteArray, 4, result, 0, result.Length);

            return result;
        }
    }
    public class PacketManager()
    {
        public delegate void MethodDelegate(byte[] data);

        private static List<byte[]> indexList = [];
        private static List<MethodDelegate> methodList = new List<MethodDelegate>();
        private static byte[] Combine(byte[] first, byte[] second, byte[] third)
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
            // Todo: discovered that it is more efficient to use a list and then convert it to an array
            int size = sendData.Length;
            byte[] data;
            data = Combine(metadata.signature, pluginSignature, sendData);

            return data;
        }
        public static bool distributePacket(SteamManager manager, byte[] packet, SendType sendType = SendType.Reliable)
        {
            for (int i = 0; i < manager.connectedPlayers.Count; i++)
            {
                manager.connectedPlayers[i].Connection.SendMessage(packet, sendType);
            }
            return true;
        }

        public static int signA = -1;
        public static int signB = 0;
        public static byte[] RegisterPlugin(MethodDelegate method)
        {
            // Results in allowing a max of 65536 plugin signatures if i did my math correctly.
            if (signA < 255)
            {
                signA += 1;
            }
            else
            {
                signB += 1;
                signA = 0;
            }

            byte[] sign = [(byte)signA, (byte)signB];

            indexList.Add(sign);
            methodList.Add(method);
            return sign;
        }
        public static void RunHandler(byte[] packet)
        {
            for (int i = 0; i < indexList.Count; i++)
            {
                if (indexList[i][0] == packet[2] && indexList[i][1] == packet[3])
                {
                    methodList[i](packet);
                    break;
                }

            }

        }
    }
}
