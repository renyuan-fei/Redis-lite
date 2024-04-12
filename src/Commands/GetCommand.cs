using System.Text;

using codecrafters_redis.Enums;
using codecrafters_redis.Interface;

namespace codecrafters_redis.Commands;

public class GetCommand : IRespCommand
{
  private readonly Dictionary<string, byte[ ]> _workingSet;
  private readonly string                      _name;

  public GetCommand(Dictionary<string, byte[ ]> workingSet, string name)
  {
    _workingSet = workingSet;
    _name = name;
  }

  public RespResponse Execute()
  {
    if (!_workingSet.TryGetValue(_name, out byte[ ]? value))
    {
      return new RespResponse(RespDataType.Null, string.Empty);
    }

    return new RespResponse(RespDataType.BulkString, Encoding.UTF8.GetString(value));
  }
}
