using System.Net;
using System.Net.Sockets;
using System.Text;

using codecrafters_redis;

// TCP server
IPAddress ipAddress = IPAddress.Any;
const int port = 6379;
TcpListener server = new TcpListener(ipAddress, port);

// Redis store
Dictionary<string, byte[]> simpleStore = new Dictionary<string, byte[]>();

// start server
try
{
  server.Start();
  Console.WriteLine("Redis-lite server is running on port 6379");

  while (true)
  {
    // create a new socket instance
    Socket socket = server.AcceptSocket();

    // start a new thread to handle the socket
    new Thread(() => HandleSocket(socket)).Start();
  }
}
catch (Exception ex)
{
  Console.WriteLine(ex.Message);
}
finally
{
  server.Stop();
}

return;

async void HandleSocket(Socket socket)
{
  byte[ ] buffer = new byte[4096];

  while (socket.Connected)
  {
    // get the data from the socket
    int received = await socket.ReceiveAsync(buffer, SocketFlags.None);

    // Check if any data was received
    if (received == 0)
    {
      break;
    }

    var factory = new RespCommandFactory(buffer, simpleStore);
    var command = factory.Create();
    var response = command.Execute();

    // Encoding the response
    byte[ ] responseData = Encoding.UTF8.GetBytes(response.GetCliResponse());

    await socket.SendAsync(responseData,SocketFlags.None);
  }

  // close the socket after sending the response
  socket.Close();
}