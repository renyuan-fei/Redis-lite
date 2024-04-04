using System.Net;
using System.Net.Sockets;
using System.Text;

// const data
const string responseTxt = "+PONG\r\n";

Console.WriteLine("Logs from your program will appear here!");

// TCP server
TcpListener server = new TcpListener(IPAddress.Any, 6379);
server.Start();

Socket socket = server.AcceptSocket();
byte[ ] requestData = new byte[1024];

socket.Receive(requestData);
byte[ ] responseData = Encoding.UTF8.GetBytes(responseTxt);

socket.Send(responseData);

socket.Close();

server.Stop();
