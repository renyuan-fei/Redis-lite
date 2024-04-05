using System.Net;
using System.Net.Sockets;
using System.Text;

// const data
const string responseTxt = "+PONG\r\n";

Console.WriteLine("Logs from your program will appear here!");

// TCP server
TcpListener server = new TcpListener(IPAddress.Any, 6379);
server.Start();

while (true)
{
  // create a new socket instance
  Socket socket = server.AcceptSocket();

  // start a new thread to handle the socket
  new Thread(() => HandleSocket(socket)).Start();
}

void HandleSocket(Socket socket)
{
  while (socket.Connected)
  {
    byte[] buffer = new byte[4096];
    int bytesRead = socket.Receive(buffer);

    // check if we got a valid response
    if (bytesRead == 0) continue;

    byte[ ] responseData = Encoding.UTF8.GetBytes(responseTxt);

    socket.Send(responseData);

    // close the socket after sending the response
    socket.Close();
  }
}
