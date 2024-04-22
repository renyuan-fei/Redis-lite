using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

using codecrafters_redis;
using codecrafters_redis.Service;

// TCP server
IPAddress ipAddress = IPAddress.Any;
int port = 6379;
ConcurrentDictionary<string, byte[ ]> simpleStore = new ConcurrentDictionary<string, byte[ ]>();

if (args.Length > 1)
{
  if (args[0] == "--port") { port = Convert.ToInt32(args[1]); }
}

RedisServer server = new RedisServer(new ExpiredTasks(simpleStore), simpleStore, port);

server.Start();
