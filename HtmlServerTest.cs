// author: mvaganov@hotmail.com
// license: Copyfree, public domain.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MrV {
	/// <summary>
	/// An HTTP server that always responds with an HTML equivalent of the HTTP request.
	/// </summary>
	class HtmlServerTest {
		public static void Main(string[] args) {
			IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8080);
			const string serverPlatform = "Apache/2.4.4 (Win32) OpenSSL/0.9.8y PHP/5.4.16";
			string stampServerStart = GetHttpTimeStampString(DateTime.UtcNow);
			TcpListener listener = new TcpListener(endpoint);
			listener.Start();
			while (!UserWantsToQuit()) {
				Task<TcpClient> clientSocketTask = listener.AcceptTcpClientAsync();
				if (!TryWaitForClientConnection(clientSocketTask)) {
					break;
				}
				LogLine("client connected: " + clientSocketTask.Result.Client.RemoteEndPoint);
				NetworkStream clientStream = clientSocketTask.Result.GetStream();
				string receivedString;
				if (!TryReadRequest(clientStream, out receivedString)) {
					break;
				}
				LogLine(receivedString, ConsoleColor.White);
				string echo = receivedString.Replace("\r\n", "<br>\n");
				TrySendResponse(clientStream, echo, serverPlatform, stampServerStart);
				clientStream.Close();
			}
		}

		public static string GetHttpTimeStampString(DateTime dateTime) {
			const string httpHeaderTimestampFormatUTC = "ddd, dd MMM yyyy HH:mm:ss";
			return dateTime.ToString(httpHeaderTimestampFormatUTC) + " GMT";
		}

		public static bool UserWantsToQuit() {
			char keyPress = GetCharNonBlocking();
			return (keyPress == 27 || keyPress == 'q');
		}

		public static char GetCharNonBlocking() {
			if (!Console.KeyAvailable) {
				return (char)0;
			}
			return Console.ReadKey(true).KeyChar;
		}

		public static void LogLine(string msg, ConsoleColor color = ConsoleColor.Green) {
			Log(msg, color);
			Console.WriteLine();
		}

		public static void Log(string message, ConsoleColor color = ConsoleColor.Yellow) {
			Console.ForegroundColor = color;
			Console.Write(message);
			Console.ResetColor();
		}

		public static bool TryWaitForClientConnection(Task<TcpClient> clientSocketTask) {
			DateTime waitStart = DateTime.UtcNow;
			while (!clientSocketTask.IsCompleted) {
				Log("waiting ... " + (DateTime.UtcNow - waitStart).Seconds + "\r");
				Thread.Sleep(1);
				if (UserWantsToQuit()) { return false; }
			}
			return true;
		}

		public static bool TryReadRequest(NetworkStream stream, out string result) {
			int bytesReceived = 0;
			List<string> inputChunks = new List<string>();
			byte[] inputBuffer = new byte[512];
			result = null;
			while (stream.DataAvailable) {
				int bytesInChunk = stream.Read(inputBuffer, 0, inputBuffer.Length);
				bytesReceived += bytesInChunk;
				string inputTextChunk = Encoding.ASCII.GetString(inputBuffer, 0, bytesInChunk);
				inputChunks.Add(inputTextChunk);
				Log("recieving " + bytesReceived + "\r");
				Thread.Sleep(1);
				if (UserWantsToQuit()) { return false; }
			}
			LogLine("received " + bytesReceived + " bytes");
			if (inputChunks.Count == 0) {
				result = "";
				return true;
			}
			result = string.Join("", inputChunks);
			return true;
		}

		public static bool TrySendResponse(NetworkStream stream, string html,
			string serverPlatform, string lastModified) {
			string timestampNow = GetHttpTimeStampString(DateTime.UtcNow);
			string[] httpHeader = {
				"HTTP/1.1 200 OK", // https://developer.mozilla.org/en-US/docs/Web/HTTP/Status
				"Date: " + timestampNow,
				"Server: " + serverPlatform,
				"Last-Modified: " + lastModified,
				"ETag: \"" + timestampNow.GetHashCode().ToString("x") + "\"",
				"Accept-Ranges: bytes",
				"Content-Length: " + html.Length,
				"Keep-Alive: timeout=5, max=100",
				"Connection: Keep-Alive",
				"Content-Type: text/html",
			};
			const string lineEnd = "\r\n";
			string htmlResponse = string.Join(lineEnd, httpHeader) + lineEnd + lineEnd + html;
			try {
				byte[] bytes = Encoding.ASCII.GetBytes(htmlResponse);
				stream.Write(bytes, 0, bytes.Length);
				stream.Flush();
			} catch { return false; }
			return true;
		}
	}
}
