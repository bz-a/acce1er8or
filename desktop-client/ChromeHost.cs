/*---------------------------------------------------------------------------------------------
*  Copyright (c) Nicolas Jinchereau. All rights reserved.
*  Licensed under the MIT License. See License.txt in the project root for license information.
*--------------------------------------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Win32;
using JsonFx;
using System.IO.Pipes;

namespace ShowdownSoftware
{
	public class ChromeMessage : Dictionary<string, object> { }

	public delegate void ChromeMessageAction(ChromeMessage message);
	public delegate void ChromeErrorAction(Exception ex);

	public class ChromeHost
	{
		static NamedPipeServerStream server = null;
		
		public static bool IsRunning { get { return server != null; } }

		public static void StartHost(ChromeMessageAction onMessage, ChromeErrorAction onError) {
			StartHostAsync(onMessage, onError);
		}

		public static void StopHost()
		{
			if(server != null)
			{
				server.Dispose();
				server = null;
			}
		}

		public static void ForwardMessage(Stream input)
		{
			using(var pipe = new NamedPipeClientStream(".", App.DomainName, PipeDirection.InOut))
			{
				try {
					pipe.Connect(5000);
				}
				catch(TimeoutException) {
					SendResponseAsync(false).Wait();
					return;
				}
				catch(IOException) {
					SendResponseAsync(false).Wait();
					return;
				}

				bool success = false;

				if(pipe.IsConnected)
				{
					byte[] len = new byte[4];
					input.Read(len, 0, len.Length);

					int msgLen = BitConverter.ToInt32(len, 0);
					byte[] msg = new byte[msgLen];
					input.Read(msg, 0, msgLen);
					
					pipe.Write(len, 0, len.Length);
					pipe.Write(msg, 0, msg.Length);
					pipe.Flush();
					success = true;
				}
				
				SendResponseAsync(success).Wait();
			}
		}

		private static async Task StartHostAsync(
			ChromeMessageAction onMessage, ChromeErrorAction onError)
		{
			RegisterHost();

			// read initial message
			var args = Environment.GetCommandLineArgs();
			if(args.Length > 1 && args[1].StartsWith("chrome-extension://"))
			{
				try {
					var msg = await ReadChromeMessageAsync(Console.OpenStandardInput());
					await SendResponseAsync(true);
					onMessage?.Invoke(msg);
				}
				catch(Exception ex) {
					await SendResponseAsync(false);
					onError?.Invoke(ex);
				}
			}

			// wait for additional messages sent from new App instances
			server = new NamedPipeServerStream(App.DomainName, PipeDirection.InOut);

			while(true)
			{
				await server.WaitForConnectionAsync();
				
				if(server == null)
					break;

				if(server.IsConnected)
				{
					try {
						var msg = await ReadChromeMessageAsync(server);
						onMessage?.Invoke(msg);
					}
					catch(Exception ex) {
						onError?.Invoke(ex);
					}
					
					server.Disconnect();
				}
			}
		}

		private static void RegisterHost()
		{
			var path = $@"Software\Google\Chrome\NativeMessagingHosts\{App.DomainName}";
			
			var val = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\nmh-manifest.json";
			var oldKey = Registry.CurrentUser.OpenSubKey(path);
			if(oldKey != null)
			{
				var oldval = oldKey.GetValue(null);
				if(oldval != null && oldval.Equals(val))
					return;
			}

			var regKey = Registry.CurrentUser.CreateSubKey(path);
			regKey.SetValue("", val);
			regKey.Close();
		}

		private static async Task SendResponseAsync(bool success)
		{
			string result = success ? "true" : "false";
			string reponse = $@"{{ ""success"": {result} }}";
			byte[] len = BitConverter.GetBytes(reponse.Length);
			byte[] res = Encoding.UTF8.GetBytes(reponse);

			Stream stream = Console.OpenStandardOutput();
			await stream.WriteAsync(len, 0, len.Length);
			await stream.WriteAsync(res, 0, res.Length);
			await stream.FlushAsync();
		}

		private static async Task<ChromeMessage> ReadChromeMessageAsync(Stream stream)
		{
			byte[] sz = new byte[4];
			int read = await stream.ReadAsync(sz, 0, 4);
			if(read != 4)
				throw new Exception("Failed to read Chrome message size");
			
			int messageSize = BitConverter.ToInt32(sz, 0);
			if(messageSize <= 0)
				throw new Exception("Chrome message is empty");

			byte[] msg = new byte[messageSize];
			read = await stream.ReadAsync(msg, 0, messageSize);
			if(read != messageSize)
				throw new Exception("Failed to read Chrome message");

			string msgString = Encoding.UTF8.GetString(msg);
			return JsonReader.Deserialize<ChromeMessage>(msgString);
		}
	}
}
