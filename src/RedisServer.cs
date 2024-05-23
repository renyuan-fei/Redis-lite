using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

using codecrafters_redis.Enums;
using codecrafters_redis.Service;
using codecrafters_redis.Type;
using codecrafters_redis.Utils;

namespace codecrafters_redis;

public class RedisServer
{
  private readonly string                                _masterHost;
  private readonly int                                   _masterPort;
  private readonly string                                _masterReplid;
  private readonly int                                   _masterReplOffset;
  private readonly RedisRole                             _role;
  private readonly IPAddress                             _ipAddress;
  private readonly int                                   _port;
  private readonly ConcurrentDictionary<string, byte[ ]> _simpleStore;
  private readonly ExpiredTasks                          _expiredTask;
  private readonly int                                   _initialTasks;
  private readonly int                                   _maxTasks;
  private          TcpClient                             _tcpClientToMaster;

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
    _role = role;
    _masterReplid = masterReplid;
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
                           100
                           );
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

      if (_role == RedisRole.Slave)
      {
        _tcpClientToMaster = new TcpClient(_masterHost, _masterPort);

        await PingToMaster();
        await ReplConf("*3\r\n$8\r\nREPLCONF\r\n$14\r\nlistening-port\r\n$4\r\n6380\r\n");
        await ReplConf("*3\r\n$8\r\nREPLCONF\r\n$4\r\ncapa\r\n$6\r\npsync2\r\n");
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
          try
          {
            await HandleSocket(socket, _expiredTask);
          }
          finally
          {
            semaphore.Release();
          }
        });
      }
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

  async private Task HandleSocket(Socket socket, ExpiredTasks expiredTask)
  {
    byte[ ] buffer = new byte[4096];

    while (socket.Connected)
    {
      // Use the Poll method to check if the connection is still active
      bool isConnected = socket.Connected && !(socket.Poll(1, SelectMode.SelectRead) && socket.Available == 0);

      if (!isConnected)
      {
        break;
      }

      // get the data from the socket
      int received = await socket.ReceiveAsync(buffer, SocketFlags.None);

      // Check if any data was received
      if (received == 0) { break; }

      var factory = new RespCommandFactory(buffer, _simpleStore, expiredTask, this);
      var command = factory.Create();
      var response = command.Execute();

      // Encoding the response
      byte[ ] responseData = Encoding.UTF8.GetBytes(response.GetCliResponse());

      await socket.SendAsync(responseData, SocketFlags.None);
    }

    // close the socket after sending the response
    socket.Close();
  }

  public string GetInfo()
  {
    var info = new Dictionary<string, string>()
    {
        { "role", _role.ToString().ToLower() },
        // {"connected_slaves", "0"},
        { "master_replid", _masterReplid },
        { "master_repl_offset", "0" },
        // {"second_repl_offset", "-1"},
        // {"repl_backlog_active", "0"},
        // {"repl_backlog_size", "1048576"},
        // {"repl_backlog_first_byte_offset", "0"},
        // {"repl_backlog_histlen", ""},
    };

    return string.Join('\n', info.Select(x => $"{x.Key}:{x.Value}"));
  }

  async private Task PingToMaster()
  {
    const string request = "*1\r\n$4\r\nping\r\n";

    NetworkStream stream = _tcpClientToMaster.GetStream();

    byte[ ] data = Encoding.ASCII.GetBytes(request);

    await stream.WriteAsync(data);

    byte[ ] buffer = new byte[1024];
    int bytesRead = await stream.ReadAsync(buffer);
    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
    Console.WriteLine(response);
  }

  async private Task ReplConf(string request)
  {
    NetworkStream stream = _tcpClientToMaster.GetStream();

    byte[ ] data = Encoding.ASCII.GetBytes(request);

    await stream.WriteAsync(data);

    byte[ ] buffer = new byte[1024];
    int bytesRead = await stream.ReadAsync(buffer);
    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
    Console.WriteLine(response);
  }
}
