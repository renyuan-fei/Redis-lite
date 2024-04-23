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
  private readonly string                                _masterReplid;
  private readonly int                                   _masterReplOffset;
  private readonly RedisRole                             _role;
  private readonly IPAddress                             _ipAddress;
  private readonly int                                   _port;
  private readonly ConcurrentDictionary<string, byte[ ]> _simpleStore;
  private readonly ExpiredTasks                          _expiredTask;

  private RedisServer(
      ExpiredTasks                          expiredTask,
      ConcurrentDictionary<string, byte[ ]> simpleStore,
      RedisRole                             role,
      string                                masterReplid,
      int                                masterReplOffset,
      int                                   port,
      IPAddress                            ipAddress)
  {
    _port = port;
    _ipAddress = ipAddress;
    _expiredTask = expiredTask;
    _simpleStore = simpleStore;
    _role = role;
    _masterReplid = masterReplid;
    _masterReplOffset = masterReplOffset;
  }

  public static RedisServer Create(
      ExpiredTasks                          expiredTask,
      ConcurrentDictionary<string, byte[ ]> simpleStore,
      RedisConfig config
      )
  {
    return new RedisServer(expiredTask,
                           simpleStore,
                           config.Role,
                           RandomStringGenerator.GenerateRandomString(40),
                           0,
                           config.Port,
                           config.IpAddress);
  }

  public void Start()
  {
    TcpListener server = new TcpListener(_ipAddress, _port);

    // start server
    try
    {
      server.Start();
      Console.WriteLine($"Redis-lite server is running on port {_port}");

      while (true)
      {
        // create a new socket instance
        Socket socket = server.AcceptSocket();

        // start a new task to handle the socket
        Task.Run(() => HandleSocket(socket, _expiredTask));
      }
    }
    catch (Exception ex) { Console.WriteLine(ex.Message); }
    finally { server.Stop(); }
  }

  async private Task HandleSocket(Socket socket, ExpiredTasks expiredTask)
  {
    byte[ ] buffer = new byte[4096];

    while (socket.Connected)
    {
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
        {"master_repl_offset", "0"},
        // {"second_repl_offset", "-1"},
        // {"repl_backlog_active", "0"},
        // {"repl_backlog_size", "1048576"},
        // {"repl_backlog_first_byte_offset", "0"},
        // {"repl_backlog_histlen", ""},
    };

    return string.Join('\n', info.Select(x => $"{x.Key}:{x.Value}"));
  }
}
