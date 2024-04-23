using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

using codecrafters_redis;
using codecrafters_redis.Enums;
using codecrafters_redis.Service;

// TCP server

ConcurrentDictionary<string, byte[ ]> simpleStore = new ConcurrentDictionary<string, byte[ ]>();

var config = OptionParser.Parse(args);

RedisServer server = RedisServer.Create(new ExpiredTasks(simpleStore),
                                        simpleStore,
                                        config);

server.Start();
