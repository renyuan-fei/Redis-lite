using System.Net;

using codecrafters_redis.Enums;

namespace codecrafters_redis.Type;

public class RedisConfig
{
  public int       Port { get; set; } = 6379;
  public RedisRole Role { get; set; } = RedisRole.Master;

  public IPAddress IpAddress { get; set; } = IPAddress.Any;
}
