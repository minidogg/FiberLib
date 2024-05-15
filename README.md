![logo](https://raw.githubusercontent.com/minidogg/FiberLib/main/media/logo(1).svg)
# FiberLib
FiberLib is a library mod for sending and receiving packets from a Bopl Battle Mod. It is currently in a bare bones state and only includes necessary tools, but will soon include more utilities for making the developer experience simpler.

## Documentation
(This project still under development, so API can change)

1. First make a global static variable for signature to send/recive packets
```cs
public static Signature signature;
```
2. Register a packet recive handler
```cs
public class MyMod : BaseUnityPlugin
{
	private void ReciveHandler(Packet packet, Connection connection, NetIdentity identity)
	{
		byte[] data = packet.data;
		Console.WriteLine($"recived {data.length} bytes");
	}

	private void Awake()
	{
		signature = PacketManager.RegisterPacketReciveHandler(ReciveHandler);
	}
}
```
3. Send Packet
```cs
public class MyMod : BaseUnityPlugin
{
	private void OnGUI()
	{
		if (GUI.Button(new Rect(0, 50, 170f, 30f), "Send Packet"))
		{
			Console.WriteLine("Sending packet!");
			Packet packet = new Packet(signature, Encoding.UTF8.GetBytes("Hello, World!"))
			PacketManager.SendPacket(packet);
		}
	}
}
```
