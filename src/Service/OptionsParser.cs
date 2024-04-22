using codecrafters_redis.Enums;
using codecrafters_redis.Type;

namespace codecrafters_redis.Service;

internal class OptionParser
{
  public static RedisConfig Parse(string[ ] args)
  {
    RedisConfig config = new RedisConfig();

    for (int i = 0; i < args.Length; i++)
    {
      switch (args[i])
      {
        case "--port" when i + 1 < args.Length :
        {
          if (int.TryParse(args[i + 1], out int port)) { config.Port = port; }

          i++; // skip next item

          break;
        }

        case "--replicaof" when i + 2 < args.Length :
          // config. = args[i + 1] + " " + args[i + 2];
          config.Role = RedisRole.Slave; // change role to slave if --replicaof is specified
          i += 2;                        // skip next two items

          break;
      }
    }

    return config;
  }
}
