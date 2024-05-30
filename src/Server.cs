using System.Collections.Concurrent;

using codecrafters_redis;
using codecrafters_redis.Service;

var simpleStore = new ConcurrentDictionary<string, byte[ ]>();

var config = OptionParser.Parse(args);

var server = RedisServer.Create(new ExpiredTasks(simpleStore),
                                simpleStore,
                                config);

await server.StartAsync();
