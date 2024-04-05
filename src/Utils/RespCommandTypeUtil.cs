using codecrafters_redis.Enums;

namespace codecrafters_redis.Utils;

public class RespCommandTypeUtil
{
  public static Dictionary<string, RespCommandType> CreateCommandTypeDict()
  {
    return Enum.GetValues(typeof(RespCommandType))
               .Cast<RespCommandType>()
               .ToDictionary(commandType => commandType.ToString().ToUpper(),
                             commandType => commandType);
  }
}
