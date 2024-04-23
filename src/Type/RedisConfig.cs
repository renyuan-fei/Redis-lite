using System.Net;

using codecrafters_redis.Enums;

namespace codecrafters_redis.Type;

public class RedisConfig
{
  public int       Port { get; set; } = 6379;
  public RedisRole Role { get; set; } = RedisRole.Master;

  public IPAddress IpAddress { get; set; } = IPAddress.Any;

  public string MasterHost { get; set; } = "127.0.0.1";

  public int MasterPort { get; set; } = 6379;
}
