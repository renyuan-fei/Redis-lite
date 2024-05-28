using System.Text;

using codecrafters_redis.Enums;
using codecrafters_redis.Interface;
using codecrafters_redis.Utils;

namespace codecrafters_redis;

/*
 * Redis Serialization Protocol (RESP) is used to serialize and deserialize Redis data structures.
 * The format "*2\r\n$4\r\necho\r\n$3\r\nhey\r\n" is an example of RESP.
 *
 * Here's what each part means:
 * - "*2\r\n": This indicates the number of lines to follow. "*" is the prefix for an array, "2" is the length of the array, and "\r\n" is the line terminator.
 * - "$4\r\necho\r\n": This is the first line of data. "$" is the prefix for a string, "4" is the length of the string, "echo" is the content of the string, and "\r\n" is the line terminator.
 * - "$3\r\nhey\r\n": This is the second line of data. "$" is the prefix for a string, "3" is the length of the string, "hey" is the content of the string, and "\r\n" is the line terminator.
 *
 * So, this command is an array with two elements, the first element is the string "echo", and the second element is the string "hey". In Redis, this represents an ECHO command with the argument "hey".
 */

public class RespRequest : IRespRequest
{
  private readonly byte[ ] _bytes;

  private readonly Dictionary<string, RespCommandType> _commandType =
      RespCommandTypeUtil.CreateCommandTypeDict();

  public RespRequest(byte[ ] bytes)
  {
    _bytes = bytes;
    Parse();
  }

  public RespCommandType CommandType { get; private set; }
  public List<string>    Arguments   { get; set; } = [];

  private void Parse()
  {
    var items = SplitBytesIntoString();
    var arrayLength = GetCommandArrayLength(items);

    // skip row number eg.*2
    var arguments = items.Skip(1).ToList();
    GetCommandAndArguments(arguments, arrayLength);
  }

  private string[ ] SplitBytesIntoString()
  {
    var commandString = Encoding.UTF8.GetString(_bytes);

    return commandString.Split("\r\n");
  }

  private static int GetCommandArrayLength(IEnumerable<string> items)
  {
    var respArrayString = items.First();
    var arrayLengthString = new string(respArrayString.Skip(1).ToArray());

    if (int.TryParse(arrayLengthString, out var arrayLength)) return arrayLength;

    Console.WriteLine($"Can not parse {
      arrayLengthString
    } to int, Error in RespRequest.cs: GetCommandArrayLength()");

    throw new Exception($"Can not parse {arrayLengthString} to int");
  }

  private void GetCommandAndArguments(
      IReadOnlyCollection<string> arguments,
      int                         arrayLength)
  {
    for (var i = 0; i < arrayLength; i++)
    {
      // command and argument
      var segment = arguments.Take(2).ToList();

      // skip the previous segment
      arguments = arguments.Skip(2).ToList();

      var sizeInfo = segment[0];
      var argument = segment[1];

      // set the command type
      if (i == 0)
      {
        CommandType = ParseCommandType(argument);

        continue;
      }

      Arguments.Add(argument);
    }

    // Console.WriteLine("command type: " + CommandType);
    // Console.WriteLine("arguments: " + string.Join(", ", Arguments));
  }

  // parses the command type from the argument
  private RespCommandType ParseCommandType(string commandTypeString)
  {
    if (!_commandType.TryGetValue(commandTypeString.ToUpper(), out var respCommandType))
    {
      throw new Exception($"Can not parse {commandTypeString} to Command Type");
    }

    return respCommandType;
  }
}
