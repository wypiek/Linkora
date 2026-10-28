using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

using WLMClient.Compat;
using WLMClient.UI.Controls;

using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;

using WLMData.Enums;
using WLMData.Data.Packets;

using WLMShared;

namespace WLMClient.UI.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.axaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Login pageLogin = null;
        private Main pageMain = null;

        /// <summary>Lets the ported code keep calling <c>window.Dispatcher.Invoke(...)</c>.</summary>
        public Dispatcher Dispatcher
        {
            get { return Dispatcher.UIThread; }
        }

        public MainWindow()
        {
            Config.Properties.SERVER_ADDRESS = ConfigurationManager.AppSettings["server_address"];
            Config.Properties.SERVER_PORT = Convert.ToInt32(ConfigurationManager.AppSettings["server_port"] ?? "1323");
            Config.Properties.REGISTRATION_URL = (ConfigurationManager.AppSettings["registration_url"] ?? "").Trim();

            // Language and theme have to be in place before any window builds itself.
            Config.SaveData settings = Config.SaveDataManager.GetConfiguration();

            Locale.Language.Load(settings.language);
            Config.Theme.Load(settings.darkTheme);

            InitializeComponent();

            SizeChanged += Window_SizeChanged;
            Closing += Window_Closing;

            Layout.Images.Load();
            Network.Client.Load(this);

            pageLogin = new Login();
            controlPanel.Children.Add(pageLogin);

            this.Icon = LoadWindowIcon(Resource.Images.Identifiers.APP_ICON_STATUS_OFFLINE);

            StartUserStatusWorker();
        }


        /// <summary>Loads one of the embedded status icons as a window icon.</summary>
        public static WindowIcon LoadWindowIcon(string url)
        {
            try
            {
                return new WindowIcon(new Bitmap(AssetLoader.Open(new Uri(url))));
            }
            catch
            {
                return null;
            }
        }

        private void StartUserStatusWorker()
        {
            Thread statusWorker = new Thread(UserStatusWorker);
            statusWorker.IsBackground = true;
            statusWorker.Start();
        }

        private void UserStatusWorker()
        {
            int olderUserStatus = 0;
            bool isUserStatusAway = false;

            while (true)
            {
                if (pageMain != null)
                {
                    int lastUpdate = (int)Locale.LastUserInput.GetLastInputTime();

                    if (lastUpdate > 150)
                    {
                        if (Locale.Personal.USER_INFO.status != (int)UserStatus.Offline && !isUserStatusAway)
                        {
                            olderUserStatus = Locale.Personal.USER_INFO.status;

                            Locale.Personal.USER_INFO.status = Convert.ToInt16(UserStatus.Away);
                            Network.Client.SendUserUpdate();

                            isUserStatusAway = true;
                        }
                    }
                    else
                    {
                        if (isUserStatusAway)
                        {
                            Locale.Personal.USER_INFO.status = olderUserStatus;
                            Network.Client.SendUserUpdate();

                            isUserStatusAway = false;
                        }
                    }
                }

                Thread.Sleep(2500);
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (pageMain != null)
            {
                pageMain.WindowSizeChanged(this.Bounds.Width, this.Bounds.Height);
            }

            if (pageLogin != null)
            {
                pageLogin.WindowSizeChanged(this.Bounds.Height);
            }
        }

        public void LoginSuccess()
        {
            // Favourites are kept per account, so they load once we know who signed in.
            Config.Favourites.LoadFor(Locale.Personal.USER_INFO == null ? "" : Locale.Personal.USER_INFO.id);

            ClearLogin();

            pageMain = new Main();
            controlPanel.Children.Add(pageMain);

            UpdateLayout();
            pageMain.UpdateLayout();
            pageMain.WindowSizeChanged(this.Bounds.Width, this.Bounds.Height);
        }

        public void ClearLogin()
        {
            controlPanel.Children.Remove(pageLogin);
            pageLogin = null;
            controlPanel.Children.Clear();
        }

        public void ConnectionClosedLogOut()
        {
            if (pageLogin == null)
            {
                Locale.ManageChatWindows.CloseAllOpenChatWindows();
                Locale.Conversations.Clear();

                pageMain = null;
                controlPanel.Children.Clear();

                pageLogin = new Login();
                controlPanel.Children.Add(pageLogin);

                UpdateLayout();
                pageLogin.UpdateLayout();
                pageLogin.WindowSizeChanged(this.Bounds.Height);

                this.Icon = LoadWindowIcon(Resource.Images.Identifiers.APP_ICON_STATUS_OFFLINE);
            }
            else
            {
                MessageBox.Show(Locale.Language.Get("error.connection.text"), Locale.Language.Get("error.connection.title"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public bool IsPageMainNull()
        {
            bool returnValue = false;

            if (pageMain == null)
            {
                returnValue = true;
            }

            return returnValue;
        }

        public Main GetMainPage()
        {
            return pageMain;
        }

        private void Window_Closing(object sender, WindowClosingEventArgs e)
        {
            if (pageMain != null)
            {
                this.WindowState = WindowState.Minimized;

                e.Cancel = true;
            }
            else
            {
                Locale.ManageChatWindows.CloseAllOpenChatWindows();

                Environment.Exit(0);
            }
        }
    }
}
