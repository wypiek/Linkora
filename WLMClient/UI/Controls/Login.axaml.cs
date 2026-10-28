using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;

using WLMClient.Compat;
using WLMClient.Config;
using WLMClient.Locale;
using WLMData.Enums;
using WLMClient.UI.Data.Enums;

namespace WLMClient.UI.Controls
{
    /// <summary>
    /// Interaction logic for Login.axaml
    /// </summary>
    public partial class Login : UserControl
    {
        private UserStatus selectedUserStatus;
        private SaveData loginConfiguration;

        /// <summary>
        /// Identifies the sign up question currently outstanding, so a late answer or the give up
        /// timer belonging to an earlier click cannot act on a later one. Zero means none.
        /// </summary>
        private int signUpRequest;

        /// <summary>Whether a connection to the server is being made in the background.</summary>
        private bool isConnecting;

        public Login()
        {
            InitializeComponent();

            checkAvailable.PointerPressed += checkAvailable_PreviewMouseLeftButtonDown;
            checkBusy.PointerPressed += checkBusy_PreviewMouseLeftButtonDown;
            checkAway.PointerPressed += checkAway_PreviewMouseLeftButtonDown;
            checkOffline.PointerPressed += checkOffline_PreviewMouseLeftButtonDown;

            checkRememberMe.Click += checkRememberMe_Click;
            checkRememberMyPassword.Click += checkRememberMyPassword_Click;
            checkSignInAutomatically.Click += checkSignInAutomatically_Click;

            btnLogin.Click += btnLogin_Click;
            txtPass.KeyUp += txtPass_PreviewKeyUp;
            txtSignUp.PointerPressed += txtSignUp_PointerPressed;

            ApplyLanguage();
            ApplyTheme();

            Language.Changed += ApplyLanguage;
            Config.Theme.Changed += ApplyTheme;

            DetachedFromVisualTree += (sender, e) =>
            {
                Language.Changed -= ApplyLanguage;
                Config.Theme.Changed -= ApplyTheme;
            };

            imagePartnerAvatar.Source = Layout.LoadResource.GetDefaultAvatarImage();
            imagePartnerFrame.Source = Layout.LoadResource.GetAvatarFrameFromStatus(UserStatus.Offline, AvatarSize.Big);

            background.Source = Layout.Images.LoadBitmap(Resource.Images.Identifiers.CHAT_WINDOW_BACKGROUND_WIDE);

            checkAvailable.Source = Layout.LoadResource.GetSmallIconFromStatus(UserStatus.Available);
            checkBusy.Source = Layout.LoadResource.GetSmallIconFromStatus(UserStatus.Busy);
            checkAway.Source = Layout.LoadResource.GetSmallIconFromStatus(UserStatus.Away);
            checkOffline.Source = Layout.LoadResource.GetSmallIconFromStatus(UserStatus.Offline);

            SetStatusSelection(UserStatus.Available);

            loginConfiguration = SaveDataManager.GetConfiguration();

            txtServer.Text = string.IsNullOrWhiteSpace(loginConfiguration.saveServer)
                ? Config.Properties.FormatServer(Config.Properties.SERVER_ADDRESS, Config.Properties.SERVER_PORT)
                : loginConfiguration.saveServer;

            if (loginConfiguration.rememberId)
            {
                txtId.Text = loginConfiguration.saveId;
                checkRememberMe.IsChecked = true;
            }

            if (loginConfiguration.rememberPassword)
            {
                txtPass.Password = loginConfiguration.savePass;
                checkRememberMyPassword.IsChecked = true;
            }

            if (loginConfiguration.autoLogin)
            {
                checkSignInAutomatically.IsChecked = true;

                // Connecting needs the control to be attached first, so defer the sign in.
                Avalonia.Threading.Dispatcher.UIThread.Post(() => btnLogin_Click(null, null),
                    Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }


        /// <summary>Repaints the sign in page for the current theme.</summary>
        private void ApplyTheme()
        {
            backgroundDimmer.Background = Config.Theme.ArtworkDimmer;
            footerArea.Background = Config.Theme.LoginFooterBackground;
            loginBox.BorderBrush = Config.Theme.Separator;

            txtSignInTitle.Foreground = Config.Theme.Accent;
            txtSignInPrompt.Foreground = Config.Theme.TextPrimary;
            txtSignUp.Foreground = Config.Theme.Accent;

            checkRememberMe.Foreground = Config.Theme.TextPrimary;
            checkRememberMyPassword.Foreground = Config.Theme.TextPrimary;
            checkSignInAutomatically.Foreground = Config.Theme.TextPrimary;
        }

        /// <summary>Applies the current language to the sign in page.</summary>
        private void ApplyLanguage()
        {
            txtSignInTitle.Text = Language.Get("login.title");
            txtSignInPrompt.Text = Language.Get("login.prompt");
            txtSignUp.Text = Language.Get("login.signup");
            txtServer.Watermark = Language.Get("login.server.watermark");
            checkRememberMe.Content = Language.Get("login.rememberme");
            checkRememberMyPassword.Content = Language.Get("login.rememberpassword");
            checkSignInAutomatically.Content = Language.Get("login.autosignin");
            btnLogin.Content = Language.Get(isConnecting ? "login.signingin" : "login.button");

            ToolTip.SetTip(txtServer, Language.Get("login.server.tooltip"));
        }

        private void checkAvailable_PreviewMouseLeftButtonDown(object sender, PointerPressedEventArgs e)
        {
            SetStatusSelection(UserStatus.Available);
        }

        private void checkBusy_PreviewMouseLeftButtonDown(object sender, PointerPressedEventArgs e)
        {
            SetStatusSelection(UserStatus.Busy);
        }

        private void checkAway_PreviewMouseLeftButtonDown(object sender, PointerPressedEventArgs e)
        {
            SetStatusSelection(UserStatus.Away);
        }

        private void checkOffline_PreviewMouseLeftButtonDown(object sender, PointerPressedEventArgs e)
        {
            SetStatusSelection(UserStatus.Offline);
        }

        private void checkRememberMe_Click(object sender, RoutedEventArgs e)
        {
            bool checkValue = ((CheckBox)sender).IsChecked.Value;

            loginConfiguration.rememberId = checkValue;
        }

        private void checkRememberMyPassword_Click(object sender, RoutedEventArgs e)
        {
            bool checkValue = ((CheckBox)sender).IsChecked.Value;

            loginConfiguration.rememberPassword = checkValue;
        }

        private void checkSignInAutomatically_Click(object sender, RoutedEventArgs e)
        {
            bool checkValue = ((CheckBox)sender).IsChecked.Value;

            loginConfiguration.autoLogin = checkValue;
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            if (isConnecting)
            {
                return;
            }

            string host;
            int port;

            if (!Config.Properties.TryParseServer(txtServer.Text, out host, out port))
            {
                MessageBox.Show(Language.Get("login.server.needed.text"),
                    Language.Get("login.server.needed.title"), MessageBoxButton.OK, MessageBoxImage.Information);

                txtServer.Focus();

                return;
            }

            Config.Properties.SERVER_ADDRESS = host;
            Config.Properties.SERVER_PORT = port;
            Config.Properties.SERVER_DISPLAY = Config.Properties.FormatServer(host, port);

            loginConfiguration.saveServer = txtServer.Text.Trim();

            if (checkRememberMe.IsChecked.Value & !checkRememberMyPassword.IsChecked.Value)
            {
                loginConfiguration.saveId = txtId.Text;
                loginConfiguration.savePass = "";
            }

            if (checkRememberMyPassword.IsChecked.Value)
            {
                loginConfiguration.saveId = txtId.Text;
                loginConfiguration.savePass = txtPass.Password;
            }

            SaveDataManager.SaveConfiguration(loginConfiguration);

            string id = txtId.Text ?? "";
            string password = txtPass.Password;
            short status = Convert.ToInt16(selectedUserStatus);

            SetConnecting(true);

            Network.Client.ConnectInBackground(connected =>
            {
                SetConnecting(false);

                if (connected)
                {
                    Network.Client.AuthenticateUser(id, password, status);
                }
            });
        }

        /// <summary>Shows that a connection is being made, and stops a second one starting alongside it.</summary>
        private void SetConnecting(bool connecting)
        {
            isConnecting = connecting;

            btnLogin.IsEnabled = !connecting;
            btnLogin.Content = Language.Get(connecting ? "login.signingin" : "login.button");
        }


        /// <summary>
        /// Opens the registration page in the user's browser. Which page that is comes from the
        /// server in the box rather than being guessed, so the link either opens a page that
        /// exists or says the server does not offer one.
        /// </summary>
        private void txtSignUp_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            string host;
            int port;

            if (!Config.Properties.TryParseServer(txtServer.Text, out host, out port))
            {
                MessageBox.Show(Language.Get("login.signup.needed.text"),
                    Language.Get("error.signup.title"), MessageBoxButton.OK, MessageBoxImage.Information);

                txtServer.Focus();

                return;
            }

            // A locally configured registration_url does not need the server's opinion.
            string configured = Config.Properties.GetRegistrationUrl(txtServer.Text, "");

            if (!string.IsNullOrWhiteSpace(configured))
            {
                UI.Data.TextParser.OpenUrl(configured);

                return;
            }

            if (isConnecting)
            {
                return;
            }

            Config.Properties.SERVER_ADDRESS = host;
            Config.Properties.SERVER_PORT = port;

            int request = ++signUpRequest;

            txtSignUp.Text = Language.Get("login.signup.asking");

            string typedServer = txtServer.Text;

            SetConnecting(true);

            Network.Client.ConnectInBackground(connected =>
            {
                SetConnecting(false);

                if (!connected)
                {
                    if (request == signUpRequest)
                    {
                        signUpRequest = 0;
                        txtSignUp.Text = Language.Get("login.signup");
                    }

                    return;
                }

                Network.Client.RequestServerInfo(info =>
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => SignUpAnswer(request, typedServer, info)));

                // A server too old to know the question, or one that never answers, must not leave
                // the link saying it is still asking.
                Avalonia.Threading.DispatcherTimer.RunOnce(() => SignUpAnswer(request, typedServer, null),
                    TimeSpan.FromSeconds(5));
            });
        }

        /// <summary>Acts on what the server said about signing up, or on it having said nothing.</summary>
        private void SignUpAnswer(int request, string typedServer, WLMData.Data.Packets.ServerInfo info)
        {
            // Whichever of the answer and the timer arrives first wins, and anything belonging to
            // an earlier click is ignored outright.
            if (request != signUpRequest)
            {
                return;
            }

            signUpRequest = 0;
            txtSignUp.Text = Language.Get("login.signup");

            string url = info == null
                ? ""
                : Config.Properties.GetRegistrationUrl(typedServer, info.registrationUrl);

            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show(Language.Format("login.signup.unavailable.text", typedServer.Trim()),
                    Language.Get("login.signup.unavailable.title"),
                    MessageBoxButton.OK, MessageBoxImage.Information);

                return;
            }

            UI.Data.TextParser.OpenUrl(url);
        }

        private void txtPass_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                btnLogin_Click(null, null);
            }
        }

        public void WindowSizeChanged(double height)
        {
            this.Height = height;
        }

        private void SetStatusSelection(UserStatus status)
        {
            borderAvailable.BorderThickness = new Thickness(0);
            borderBusy.BorderThickness = new Thickness(0);
            borderOffline.BorderThickness = new Thickness(0);
            borderAway.BorderThickness = new Thickness(0);

            if (status == UserStatus.Available)
            {
                borderAvailable.BorderThickness = new Thickness(3);
            }

            if (status == UserStatus.Busy)
            {
                borderBusy.BorderThickness = new Thickness(3);
            }

            if (status == UserStatus.Away)
            {
                borderAway.BorderThickness = new Thickness(3);
            }

            if (status == UserStatus.Offline)
            {
                borderOffline.BorderThickness = new Thickness(3);
            }

            selectedUserStatus = status;
        }
    }
}
