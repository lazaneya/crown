using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using CrownRelayServer;
using Godot;
using ProtoBuf;

public partial class RelayServer : Node, IRelayedObject
{
  // Debug purposes, remove later
  static string ServerEP = "129.146.116.47:10013";
  static string LocalEP = "192.168.192.134:10013";
  // Debug purposes, remove later

  // Server Information
  static IPEndPoint relayServer = IPEndPoint.Parse(LocalEP);
  static IPEndPoint server;
  static Socket serverSocket;
  static byte[] dataStream = new byte[1024];

  // Instance Registry
  static Dictionary<Type, List<Node>> InstanceRegistry = new();
  public string RelayedID { get; } = "0";

  // Getters
  public static bool IsActive
  {
    get
    {
      return serverSocket != null;
    }
  }

  // Props
  public static int LocalPlayerNumber = 0;
  private static TaskCompletionSource<bool> PlayerNumberPendingSource;
  public static TaskCompletionSource<bool> LobbyValidationPendingSource { get; private set;  }
  static int LobbyId;

  // RPC CALLABLE ONLY
  private void SetPlayerNumber(PCommand command)
  {
    LocalPlayerNumber = command.Kwargs["playerNumber"].ToInt();
    PlayerNumberPendingSource?.TrySetResult(true);
  }

  public override void _Ready()
  {
    // register relay server instance, to handle all private RPC calls
    RegisterInstance(this);
  }

  static public void RegisterInstance(Node obj)
  {
    if (!InstanceRegistry.ContainsKey(obj.GetType()))
    {
      InstanceRegistry[obj.GetType()] = new List<Node>() { obj };
    }
    else
    {
      InstanceRegistry[obj.GetType()].Add(obj);
    }
  }

  static public Task<int> RelayCommand(object command)
  {
    byte[] buffer;
    using (MemoryStream ms = new MemoryStream())
    {
      Serializer.Serialize(ms, command);
      buffer = ms.ToArray();
    }

    return serverSocket.SendToAsync(buffer, SocketFlags.None, relayServer);
  }

  static public async void HostServer()
  {
    // Open Socket to Accept Traffic
    OpenSocket();

    LobbyValidationPendingSource = new TaskCompletionSource<bool>();

    GD.Print($"Connecting to RelayServer, {relayServer.Address}:{relayServer.Port}");

    await RelayCommand(new PCommand
    {
      Destination = PDestination.RelayServer,
      PlayerName = GameManager.LocalPlayer.PlayerName,
      IsHost = true
    });

    // Display host controls once they've properly joined the game
    await LobbyValidationPendingSource.Task;
    GameManager.LocalPlayer.UI.DisplayHostControls();
    GameManager.LocalPlayer.UI.DisplayLobby();
  }

  static public async void JoinServer(int lobbyId)
  {
    OpenSocket();

    PlayerNumberPendingSource = new TaskCompletionSource<bool>();
    LobbyValidationPendingSource = new TaskCompletionSource<bool>();

    await RelayCommand(new PCommand
    {
      Destination = PDestination.RelayServer,
      PlayerName = GameManager.LocalPlayer.PlayerName,
      LobbyId = lobbyId,
      IsHost = false
    });

		await LobbyValidationPendingSource.Task;
    GameManager.UI.DisplayLobby();
  }

  static private void OpenSocket()
  {
    // Create Socket
    serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

    // Get server ip address
    server = new IPEndPoint(IPAddress.Any, 0);

    // Bind Socket to EndPoint
    serverSocket.Bind(server);
    GD.Print($"Created Socket at, {server.Address}:{server.Port}");

    // Cast for reference
    EndPoint relayServerEP = relayServer;

    // Start accepting traffic from the relay server
    serverSocket.BeginReceiveFrom(dataStream, 0, dataStream.Length, SocketFlags.None, ref relayServerEP, new AsyncCallback(ReceiveData), relayServer);
  }

  static void ReceiveData(IAsyncResult asyncResult)
  {
    EndPoint relayServerEP = relayServer;

    try
    {
      int byteCount = serverSocket.EndReceiveFrom(asyncResult, ref relayServerEP);
      PCommand command = CatchCommand(byteCount);
      GD.Print($"Bytes: {byteCount}, {command.ObjectTypeString}::{command.MethodStringName} Kwargs: {command.Kwargs.Count} Args: {command.Args.Length}");

      // process the request on a background thread
      Task.Run(() =>
      {
        InvokeRpc(command);
      });

      // re-open socket to receive data
      serverSocket.BeginReceiveFrom(dataStream, 0, dataStream.Length, SocketFlags.None, ref relayServerEP, new AsyncCallback(ReceiveData), relayServer);
    }
    catch (Exception ex)
    {
      GD.Print($"ReceiveData Error : {ex.Message}");
    }
  }

  // Call function on all clients
  public static void Rpc(PCommand command)
  {
    command.Destination = PDestination.All;
    command.PlayerName = GameManager.LocalPlayer.PlayerName;
    RelayCommand(command);
  }

  // Call function only on host
  public static void RpcHost(PCommand command)
  {
    command.Destination = PDestination.Host;
    command.PlayerName = GameManager.LocalPlayer.PlayerName;
    RelayCommand(command);
  }

  public static void RpcClients(PCommand command)
  {
    command.Destination = PDestination.Clients;
    command.PlayerName = GameManager.LocalPlayer.PlayerName;
    RelayCommand(command);
  }

  // Process Received Data
  static PCommand CatchCommand(int byteCount)
  { 
    PCommand command = null;

    using (MemoryStream ms = new MemoryStream(dataStream, 0, byteCount))
    {
      command = Serializer.Deserialize<PCommand>(ms);
    }

    return command;
  }

  // Update Player in GameManager
  static private async void ValidateLobbyInfo(PCommand command)
  {
    if (PlayerNumberPendingSource is not null) await PlayerNumberPendingSource.Task;

    // Parse Players From Lobby Info.
    string[] playerNames = command.Args;

    GameManager.UpdatePlayers(playerNames);

    bool wasConnectingToServer = LobbyValidationPendingSource.TrySetResult(true);
    if (wasConnectingToServer)
    {
      GD.Print($"{GameManager.LocalPlayer.PlayerName} Successfully Connected");
    }
  }

  // Handles calling the received rpc call
  static void InvokeRpc(PCommand command)
  {
    Type type = Type.GetType(command.ObjectTypeString);

    List<Node> typeInstances = InstanceRegistry[type];
    
    Node instance = null;

    if (typeInstances.Count() == 1)
    {
      instance = typeInstances[0];
    }
    else if (typeInstances.Count() > 1)
    {
      instance = typeInstances.Find(s => (s as IRelayedObject).RelayedID == command.ObjectID);
    }

    if (instance == null)
    {
      GD.Print($"RPC Error -- Instance {command.ObjectID}, not found in {type} registry.");
    }

    if (command.Kwargs.Count == 0 && command.Args.Length == 0)
    {
      instance.CallDeferred(command.MethodStringName);
      return;
    }

    instance.CallDeferred(command.MethodStringName, command);
  }
}
