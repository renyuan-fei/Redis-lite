using System.ComponentModel.DataAnnotations;

using codecrafters_redis.Enums;
using codecrafters_redis.Interface;

namespace codecrafters_redis;

public record RespResponse(RespDataType DataType, string Message) : IRespResponse
{
  private const string Suffix = "\r\n";

  private readonly Dictionary<RespDataType, char> _respTypeDict = new()
  {
      { RespDataType.SimpleString, '+' },
      { RespDataType.SimpleError, '-' },
      { RespDataType.Integer, ':' },
      { RespDataType.BulkString, '$' },
      { RespDataType.Array, '*' },
      { RespDataType.Null, '$' },
      { RespDataType.Boolean, '#' },
      { RespDataType.Double, ',' },
      { RespDataType.BigNumber, '(' },
      { RespDataType.BulkError, '!' },
      { RespDataType.VerbatimString, '=' },
      { RespDataType.Map, '%' },
      { RespDataType.Set, '~' },
      { RespDataType.Push, '>' },
  };

  public string GetCliResponse() { return $"{GetPrefix()}{Message}{Suffix}"; }

  private string GetPrefix()
  {
    var sign = _respTypeDict[DataType];

    var additionalPrefix = DataType switch
    {
        RespDataType.BulkString => $"{Message.Length}{Suffix}",
        RespDataType.Null => "-1",
        _                       => string.Empty
    };

    var tm = string.IsNullOrEmpty(additionalPrefix)
        ? $"{sign}"
        : $"{sign}{additionalPrefix}";
    Console.WriteLine(tm);
    return tm;
  }
}
