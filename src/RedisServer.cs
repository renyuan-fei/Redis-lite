using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

using codecrafters_redis.Enums;
using codecrafters_redis.Interface;
using codecrafters_redis.Service;
using codecrafters_redis.Type;
using codecrafters_redis.Utils;

namespace codecrafters_redis;

public class RedisServer
{
  private readonly ExpiredTasks                          _expiredTask;
  private readonly int                                   _initialTasks;
  private readonly IPAddress                             _ipAddress;
  private readonly string                                _masterHost;
  private readonly int                                   _masterPort;
  private readonly int                                   _masterReplOffset;
  private readonly int                                   _maxTasks;
  private readonly int                                   _port;
  public           RedisRole                             Role { get; }
  private readonly ConcurrentDictionary<string, byte[ ]> _simpleStore;
  private          Socket                                _socketToMaster;
  private readonly ConcurrentBag<Socket>                 _connectedReplicas = [];
  public           string                                MasterReplid { get; }

  private RedisServer(
      ExpiredTasks                          expiredTask,
      ConcurrentDictionary<string, byte[ ]> simpleStore,
      RedisRole                             role,
      string                                masterReplid,
      int                                   masterReplOffset,
      int                                   port,
      IPAddress                             ipAddress,
      string                                masterHost,
      int                                   masterPort,
      int                                   initialTasks,
      int                                   maxTasks)
  {
    _port = port;
    _ipAddress = ipAddress;
    _masterPort = masterPort;
    _masterHost = masterHost;
    _expiredTask = expiredTask;
    _simpleStore = simpleStore;
    Role = role;
    MasterReplid = masterReplid;
    _masterReplOffset = masterReplOffset;
    _initialTasks = initialTasks;
    _maxTasks = maxTasks;
  }


  public static RedisServer Create(
      ExpiredTasks                          expiredTask,
      ConcurrentDictionary<string, byte[ ]> simpleStore,
      RedisConfig                           config
      )
  {
    return new RedisServer(expiredTask,
                           simpleStore,
                           config.Role,
                           RandomStringGenerator.GenerateRandomString(40),
                           0,
                           config.Port,
                           config.IpAddress,
                           config.MasterHost,
                           config.MasterPort,
                           10,
                           100);
  }

  public async Task StartAsync()
  {
    TcpListener server = new TcpListener(_ipAddress, _port);

    // Maximum number of concurrent tasks
    SemaphoreSlim semaphore = new SemaphoreSlim(_initialTasks, _maxTasks);

    // start server
    try
    {
      server.Start();
      Console.WriteLine($"Redis-lite server is running on port {_port}");

      if (Role == RedisRole.Slave) { await InitializeSlaveRoleAsync(); }

      await AcceptClientConnections(server, semaphore);
    }
    catch (Exception ex)
    {
      Console.WriteLine(ex.Message);
      // Release the semaphore if an exception occurs
      semaphore.Release();
    }
    finally
    {
      server.Stop();
    }
  }

  async private Task InitializeSlaveRoleAsync()
  {
    await ConnectToMasterAsync();
    // await HandleRdbFileAsync(_socketToMaster);
    await HandleSocketAsync(_socketToMaster, _expiredTask);
  }

  async private Task AcceptClientConnections(TcpListener server, SemaphoreSlim semaphore)
  {
    while (true)
    {
      try
      {
        // Make sure the number of concurrent tasks does not exceed the maximum
        await semaphore.WaitAsync();
        Socket clientSocket = await server.AcceptSocketAsync();

        _ = Task.Run(async () =>
        {
          try
          {
            await HandleSocketAsync(clientSocket, _expiredTask);
          }
          finally
          {
            // Release the semaphore after the task is completed
            semaphore.Release();
          }
        });
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error in accepting connections: {ex.Message}");
      }
    }
  }

  private static bool IsSocketConnected(Socket socket)
  {
    // Use the Poll method to check if the connection is still active
    return socket.Connected && !(socket.Poll(1, SelectMode.SelectRead) && socket.Available == 0);
  }

  async private static Task SendResponse(Socket socket, IRespResponse response)
  {
    // Encoding the response
    byte[] responseData = Encoding.UTF8.GetBytes(response.GetCliResponse());
    await socket.SendAsync(responseData, SocketFlags.None);
  }

  async private Task HandleSocketAsync(Socket socket, ExpiredTasks expiredTask)
  {
    var buffer = new byte[4096];

    while (socket.Connected)
    {
      if (!IsSocketConnected(socket)) { break; }

      // get the data from the socket
      var received = await socket.ReceiveAsync(buffer, SocketFlags.None);

      // Check if any data was received
      if (received == 0) { break; }

      RespCommandFactory factory = new RespCommandFactory(buffer, _simpleStore, expiredTask, this);
      var command = factory.Create();
      var response = command.Execute();

      // If the command is from the master, do not send the response back
      if (Role == RedisRole.Slave)continue;

      await SendResponse(socket, response);

      // If the command implements IPostExecutionCommand, execute the PostExecutionAction
      if (command is not IPostExecutionCommand postExecutionCommand) continue;

      postExecutionCommand.PostExecutionAction?.Invoke(socket);

      // After the PSYNC command is executed, add the replica to the list of connected replicas
      _connectedReplicas.Add(socket);
    }

    // close the socket after sending the response
    socket.Close();
  }

  public string GetInfo()
  {
    Dictionary<string, string> info = new Dictionary<string, string>
    {
        { "role", Role.ToString().ToLower() },
        // {"connected_slaves", "0"},
        { "master_replid", MasterReplid },
        { "master_repl_offset", "0" }
        // {"second_repl_offset", "-1"},
        // {"repl_backlog_active", "0"},
        // {"repl_backlog_size", "1048576"},
        // {"repl_backlog_first_byte_offset", "0"},
        // {"repl_backlog_histlen", ""},
    };

    return string.Join('\n', info.Select(x => $"{x.Key}:{x.Value}"));
  }

  async private Task ConnectToMasterAsync()
  {
    _socketToMaster = new Socket(SocketType.Stream, ProtocolType.Tcp);

    while (!_socketToMaster.Connected)
    {
      try
      {
        await _socketToMaster.ConnectAsync(_masterHost, _masterPort);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Failed to connect to master: {ex.Message}");
        Console.WriteLine("Retrying in 5 seconds...");
        await Task.Delay(5000);
      }
    }

    await SendInitialCommandsToMaster();
  }

  async private Task SendInitialCommandsToMaster()
  {
    await SendCommandToMasterAsync("*1\r\n$4\r\nping\r\n");

    await SendCommandToMasterAsync($"*3\r\n$8\r\nREPLCONF\r\n$14\r\nlistening-port\r\n$4\r\n{_port}\r\n");

    await SendCommandToMasterAsync("*3\r\n$8\r\nREPLCONF\r\n$4\r\ncapa\r\n$6\r\npsync2\r\n");

    await SendCommandToMasterAsync("*3\r\n$5\r\nPSYNC\r\n$1\r\n?\r\n$2\r\n-1\r\n");

    await HandleRdbFileAsync(_socketToMaster);
  }

  async private Task SendCommandToMasterAsync(string request)
  {
    byte[ ] data = Encoding.ASCII.GetBytes(request);
    ArraySegment<byte> sendSegment = new ArraySegment<byte>(data);

    // Send data
    await _socketToMaster.SendAsync(sendSegment, SocketFlags.None);

    byte[ ] buffer = new byte[1024];
    ArraySegment<byte> receiveSegment = new ArraySegment<byte>(buffer);

    // Receive data
    int received = await _socketToMaster.ReceiveAsync(receiveSegment, SocketFlags.None);

    string response = Encoding.UTF8.GetString(buffer, 0, received);
    Console.WriteLine(response);
  }

  public async void PropagateCommandToReplicas(string command)
  {
    // Convert the command to RESP format
    string resp = RedisCommandConverter.ToRespFormat(command);
    byte[] commandData = Encoding.UTF8.GetBytes(resp);

    foreach (var replica in _connectedReplicas)
    {
      if (IsSocketConnected(replica))
      {
        try
        {
          await replica.SendAsync(commandData, SocketFlags.None);
          Console.WriteLine($"Command propagated to replica: {command}");
        }
        catch (Exception ex)
        {
          Console.WriteLine($"Failed to send command to replica: {ex.Message}");
          _connectedReplicas.TryTake(out _);
        }
      }
      else
      {
        Console.WriteLine($"Replica not connected, failed to send command: {command}");
        _connectedReplicas.TryTake(out _);
      }
    }
  }

  async private static Task HandleRdbFileAsync(Socket socket)
  {
    byte[] buffer = new byte[4096];
    var received = await socket.ReceiveAsync(buffer, SocketFlags.None);

    // Process the RDB file data

    Console.WriteLine(received > 0
                          ? "RDB file data received and being processed."
                          : "No RDB file data received or connection closed.");

    return;
  }
}
