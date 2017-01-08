/*---------------------------------------------------------------------------------------------
*  Copyright (c) Nicolas Jinchereau. All rights reserved.
*  Licensed under the MIT License. See License.txt in the project root for license information.
*--------------------------------------------------------------------------------------------*/

using System;
using System.Threading;
using System.Windows;

namespace ShowdownSoftware
{
	public partial class App : Application
	{
		public const string DomainName = "com.showdownsoftware.acce1er8or";
		public const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/55.0.2883.87 Safari/537.36";
		
		static Mutex mutex = new Mutex(true, App.DomainName);

		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);
			var window = new MainWindow();
			window.Show();
		}

		[STAThread]
		public static void Main(string[] args)
		{
			try
			{
				if(mutex.WaitOne(TimeSpan.Zero, true))
				{
					try {
						App app = new App();
						app.Run();
					}
					finally {
						mutex.ReleaseMutex();
					}
				}
				else
				{
					if(args.Length > 0 && args[0].StartsWith("chrome-extension://"))
					{
						ChromeHost.ForwardMessage(Console.OpenStandardInput());
					}
				}
			}
			catch(Exception ex) {
				MessageBox.Show(ex.ToString());
			}
		}
	}
}
