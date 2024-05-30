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

  public string MasterReplid { get; }

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

      if (Role == RedisRole.Slave)
      {
        await SendCommandsToMaster();
        await HandleRdbFile(_socketToMaster);
        await HandleSocket(_socketToMaster, _expiredTask);
      }

      while (true)
      {
        // Wait for a free slot
        await semaphore.WaitAsync();

        // create a new socket instance
        Socket socket = await server.AcceptSocketAsync();

        // Process the connection in a separate task
        _ = Task.Run(async () =>
        {
          try { await HandleSocket(socket, _expiredTask); }
          finally { semaphore.Release(); }
        });
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine(ex.Message);

      // Release the semaphore if an exception occurs
      semaphore.Release();
    }
    finally { server.Stop(); }
  }

  async private Task HandleSocket(Socket socket, ExpiredTasks expiredTask)
  {
    var buffer = new byte[4096];

    while (socket.Connected)
    {
      // Use the Poll method to check if the connection is still active
      bool isConnected = socket.Connected
                      && !(socket.Poll(1, SelectMode.SelectRead)
                        && socket.Available == 0);

      if (!isConnected) { break; }

      // get the data from the socket
      var received = await socket.ReceiveAsync(buffer, SocketFlags.None);

      // Check if any data was received
      if (received == 0) { break; }

      RespCommandFactory factory = new RespCommandFactory(buffer, _simpleStore, expiredTask, this);

      var command = factory.Create();
      var response = command.Execute();

      // // If the command is from the master, we do not send the response back
      if (Role == RedisRole.Slave)
      {
        continue; // Do not send response back to the master
      }

      // Encoding the response
      byte[ ] responseData = Encoding.UTF8.GetBytes(response.GetCliResponse());

      await socket.SendAsync(responseData, SocketFlags.None);

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

  async private Task SendCommandsToMaster()
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
        await Task.Delay(5000); //wait for 5 seconds before retrying
      }
    }

    await SendCommandToMasterAsync("*1\r\n$4\r\nping\r\n");

    await SendCommandToMasterAsync($"*3\r\n$8\r\nREPLCONF\r\n$14\r\nlistening-port\r\n$4\r\n{_port}\r\n");

    await SendCommandToMasterAsync("*3\r\n$8\r\nREPLCONF\r\n$4\r\ncapa\r\n$6\r\npsync2\r\n");

    await SendCommandToMasterAsync("*3\r\n$5\r\nPSYNC\r\n$1\r\n?\r\n$2\r\n-1\r\n");

    // handle socket here
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
    string resp = RedisCommandConverter.ToRespFormat(command);

    byte[ ] commandData = Encoding.UTF8.GetBytes(resp);

    foreach (var replica in _connectedReplicas)
    {
      var socket = replica;

      if (socket.Connected
       && !(socket.Poll(1, SelectMode.SelectRead) && socket.Available == 0))
      {
        try
        {
          await socket.SendAsync(commandData);
          Console.WriteLine($"Command propagated to replica: {resp} ");
        }
        catch (Exception ex)
        {
          Console.WriteLine($"Failed to send command to replica: {
            ex.Message
          }, please check the connection");

          _connectedReplicas.TryTake(out socket); // Remove the disconnected replica
        }
      }
      else
      {
        Console.WriteLine($"Failed to send command to replica: {
          command
        }, replica is not connected");

        _connectedReplicas.TryTake(out socket); // Remove the disconnected replica
      }
    }
  }
  async private Task HandleRdbFile(Socket socket)
  {
    byte[] buffer = new byte[4096];
    var received = await socket.ReceiveAsync(buffer, SocketFlags.None);

    if (received > 0)
    {
      Console.WriteLine("RDB file data received and being processed.");
    }
    else
    {
      Console.WriteLine("No RDB file data received or connection closed.");
    }
  }

}
