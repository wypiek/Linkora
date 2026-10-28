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
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Layout;
using Avalonia.VisualTree;

using WLMClient.Compat;
using WLMClient.Compat.Documents;

using WLMData.Data.Packets;
using WLMClient.UI.Data;
using WLMClient.UI.Data.Enums;
using WLMClient.Locale;
using WLMData.Enums;
using WLMClient.Layout;
using WLMClient.UI.Controls.Dialogs;
using WLMClient.Network;

using BrushConverter = WLMClient.Compat.BrushConverter;

namespace WLMClient.UI.Controls
{
    /// <summary>
    /// Interaction logic for Main.axaml
    /// </summary>
    public partial class Main : UserControl
    {
        private readonly List<ContactRow> listContacts = new List<ContactRow>();

        private ContactGroupHeader favouritesHeader;
        private ContactGroupHeader contactsHeader;

        private bool isEnterKeyDownInComment;
        private bool isCommentEditSubmitted;
        private bool isCommentBeingEdited;

        public Main()
        {
            InitializeComponent();

            CreateGroupHeaders();
            WireEvents();

            menuItemIconArrowAvailable.Source = LoadResource.GetSmallIconFromStatus(UserStatus.Available);
            menuItemIconArrowBusy.Source = LoadResource.GetSmallIconFromStatus(UserStatus.Busy);
            menuItemIconArrowAway.Source = LoadResource.GetSmallIconFromStatus(UserStatus.Away);
            menuItemIconArrowOffline.Source = LoadResource.GetSmallIconFromStatus(UserStatus.Offline);

            isEnterKeyDownInComment = false;
            isCommentEditSubmitted = false;
            isCommentBeingEdited = false;

            ApplyLanguage();
            ApplyTheme();
        }

        /// <summary>Repaints the surfaces this page draws itself.</summary>
        private void ApplyTheme()
        {
            listContactBorder.Background = Config.Theme.ListBackground;
            contactListView.Background = Config.Theme.ListBackground;

            txtQuickMessage.Background = Config.Theme.ComposeBackground;
            txtQuickMessage.Foreground = Config.Theme.TextPrimary;
            txtFriends.Foreground = Config.Theme.TextPrimary;
            txtVersion.Foreground = Config.Theme.VersionText;
            backgroundDimmer.Background = Config.Theme.ArtworkDimmer;
            footerArea.Background = Config.Theme.FooterBackground;
            footerBorder.BorderBrush = Config.Theme.FooterBorder;

            foreach (ContactRow row in listContacts)
            {
                row.ApplyTheme();
            }

            favouritesHeader.ApplyTheme();
            contactsHeader.ApplyTheme();
        }

        private void WireEvents()
        {
            btnArrowStatus.PointerPressed += btnArrowStatus_PreviewMouseLeftButtonDown;

            // Escape has to be intercepted before the text box acts on it, hence the tunnelling.
            txtQuickMessage.AddHandler(InputElement.KeyDownEvent, txtQuickMessage_PreviewKeyDown,
                RoutingStrategies.Tunnel);
            txtQuickMessage.KeyUp += txtQuickMessage_PreviewKeyUp;

            // Tunnelling as well: the text box handles its own pointer presses, so an ordinary
            // handler never ran, the box was never marked as being edited, and taking focus
            // promptly gave it away again - nothing typed ever reached it.
            txtQuickMessage.AddHandler(InputElement.PointerPressedEvent,
                txtQuickMessage_PreviewMouseLeftButtonDown, RoutingStrategies.Tunnel);
            txtQuickMessage.LostFocus += txtQuickMessage_LostKeyboardFocus;
            txtQuickMessage.GotFocus += txtQuickMessage_GotKeyboardFocus;

            btnAddFriend.Click += btnAddFriend_Click;
            btnAddFriend.PointerEntered += btnAddFriend_MouseEnter;
            btnAddFriend.PointerExited += btnAddFriend_MouseLeave;

            menuItemArrowAvailable.Click += menuItemArrowAvailable_Click;
            menuItemArrowBusy.Click += menuItemArrowBusy_Click;
            menuItemArrowAway.Click += menuItemArrowAway_Click;
            menuItemArrowOffline.Click += menuItemArrowOffline_Click;
            menuItemArrowOptions.Click += menuItemArrowOptions_Click;
            menuItemArrowExit.Click += menuItemArrowExit_Click;

            Config.Theme.Changed += ApplyTheme;
            Conversations.Changed += OnConversationChanged;
            Config.Favourites.Changed += RebuildContactList;
            Language.Changed += ApplyLanguage;

            DetachedFromVisualTree += (sender, e) =>
            {
                Config.Theme.Changed -= ApplyTheme;
                Conversations.Changed -= OnConversationChanged;
                Config.Favourites.Changed -= RebuildContactList;
                Language.Changed -= ApplyLanguage;
            };
        }

        private void CreateGroupHeaders()
        {
            favouritesHeader = new ContactGroupHeader();
            favouritesHeader.Toggled += RebuildContactList;

            contactsHeader = new ContactGroupHeader();
            contactsHeader.Toggled += RebuildContactList;
        }

        #region Language

        /// <summary>Applies the current language to everything shown on this page.</summary>
        private void ApplyLanguage()
        {
            menuItemArrowAvailable.Header = Language.Get("menu.available");
            menuItemArrowBusy.Header = Language.Get("menu.busy");
            menuItemArrowAway.Header = Language.Get("menu.away");
            menuItemArrowOffline.Header = Language.Get("menu.appearoffline");
            menuItemArrowOptions.Header = Language.Get("menu.options");
            menuItemArrowExit.Header = Language.Get("menu.exit");

            txtVersion.Text = Language.Format("main.version", Config.AppVersion.Display);

            ToolTip.SetTip(btnAddFriend, Language.Get("main.addfriend"));
            ToolTip.SetTip(txtConnectedTo, Language.Get("main.connectedto.tooltip"));

            UpdatePersonalInformation();
            RebuildContactList();
        }

        #endregion

        private void btnArrowStatus_PreviewMouseLeftButtonDown(object sender, PointerPressedEventArgs e)
        {
            contextMenuArrow.Open(btnArrowStatus);
        }

        private void txtQuickMessage_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                isEnterKeyDownInComment = true;
                isCommentBeingEdited = false;
            }

            if (e.Key == Key.Escape)
            {
                ResetComment();
                isCommentBeingEdited = false;
                e.Handled = true;
            }
        }

        private void txtQuickMessage_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (isEnterKeyDownInComment)
            {
                isCommentEditSubmitted = true;
                txtQuickMessage.SelectionStart = 0;
                Keyboard.ClearFocus(this);
                Personal.USER_INFO.comment = (txtQuickMessage.Text ?? "").Trim();
                isEnterKeyDownInComment = false;

                Client.SendUserUpdate();
                UpdatePersonalInformation();

                isCommentBeingEdited = false;

                ResetComment();
            }
        }

        private void txtQuickMessage_PreviewMouseLeftButtonDown(object sender, PointerPressedEventArgs e)
        {
            // Marked first: focusing the box runs GotFocus straight away, which gives the focus up
            // again and puts the placeholder back unless an edit is already under way.
            isCommentBeingEdited = true;

            if (Personal.USER_INFO.comment.Length == 0)
            {
                txtQuickMessage.Clear();
                txtQuickMessage.Focus();
            }
        }

        private void txtQuickMessage_LostKeyboardFocus(object sender, RoutedEventArgs e)
        {
            ResetComment();
        }

        public void ResetComment()
        {
            if (!isCommentEditSubmitted)
            {
                if (Personal.USER_INFO.comment.Length == 0)
                {
                    txtQuickMessage.Text = Language.Get("main.quickmessage");
                }
                else
                {
                    txtQuickMessage.Text = Personal.USER_INFO.comment;
                }
            }

            Keyboard.ClearFocus(this);
            isCommentBeingEdited = false;
            isCommentEditSubmitted = false;
        }

        public void WindowSizeChanged(double width, double height)
        {
            if (width >= 555)
            {
                background.Source = Images.LoadBitmap(Resource.Images.Identifiers.CHAT_WINDOW_BACKGROUND_WIDE);
            }

            if (width < 555)
            {
                background.Source = Images.LoadBitmap(Resource.Images.Identifiers.CHAT_WINDOW_BACKGROUND_SKINNY);
            }

            ResizeContactRows();

            mainControl.Height = height;
        }

        /// <summary>Keeps the rows and their headings as wide as the list.</summary>
        private void ResizeContactRows()
        {
            double width = listContactBorder.Bounds.Width - 2;

            if (width <= 0)
            {
                return;
            }

            foreach (ContactRow row in listContacts)
            {
                row.Width = width;
            }

            favouritesHeader.Width = width;
            contactsHeader.Width = width;
        }

        public void UpdatePersonalInformation()
        {
            txtName.Text = Personal.USER_INFO.name;
            txtStatus.Text = "(" + Language.GetStatus((UserStatus)Personal.USER_INFO.status) + ")";
            txtConnectedTo.Text = Language.Format("main.connectedto", Config.Properties.GetServerDisplay());

            if (Personal.USER_INFO.comment.Length != 0)
            {
                txtQuickMessage.Text = Personal.USER_INFO.comment;
            }
            else
            {
                txtQuickMessage.Text = Language.Get("main.quickmessage");
            }

            imagePartnerFrame.Source = LoadResource.GetAvatarFrameFromStatus((UserStatus)Personal.USER_INFO.status, AvatarSize.Small);

            if (Personal.USER_INFO.avatar != "")
            {
                Bitmap image = LoadResource.GetAvatar(Personal.USER_INFO.avatar);

                if (image != null)
                {
                    imagePartnerAvatar.Source = LoadResource.Resize(image, 50, 50, BitmapInterpolationMode.HighQuality);
                }
                else
                {
                    imagePartnerAvatar.Source = LoadResource.Resize(LoadResource.GetDefaultAvatarImage(), 50, 50, BitmapInterpolationMode.HighQuality);
                }
            }
            else
            {
                imagePartnerAvatar.Source = LoadResource.Resize(LoadResource.GetDefaultAvatarImage(), 50, 50, BitmapInterpolationMode.HighQuality);
            }

            Window mainWindow = MessageBox.GetActiveWindow();
            UI.Windows.MainWindow messengerWindow = mainWindow as UI.Windows.MainWindow;

            if (messengerWindow == null)
            {
                messengerWindow = this.GetVisualRoot() as UI.Windows.MainWindow;
            }

            if (messengerWindow != null)
            {
                if (Personal.USER_INFO.status == 0) // Offline
                {
                    messengerWindow.Icon = UI.Windows.MainWindow.LoadWindowIcon(Resource.Images.Identifiers.APP_ICON_STATUS_OFFLINE);
                }

                if (Personal.USER_INFO.status == 1) // Busy
                {
                    messengerWindow.Icon = UI.Windows.MainWindow.LoadWindowIcon(Resource.Images.Identifiers.APP_ICON_STATUS_BUSY);
                }

                if (Personal.USER_INFO.status == 2) // Away
                {
                    messengerWindow.Icon = UI.Windows.MainWindow.LoadWindowIcon(Resource.Images.Identifiers.APP_ICON_STATUS_AWAY);
                }

                if (Personal.USER_INFO.status == 3) // Available
                {
                    messengerWindow.Icon = UI.Windows.MainWindow.LoadWindowIcon(Resource.Images.Identifiers.APP_ICON_STATUS_AVAILABLE);
                }
            }

            TextParser.ParseText(txtQuickMessage, false);
            TextParser.ParseText(txtName, false);

            ManageChatWindows.UpdateChatWindowPersonal();
        }

        #region Contact list

        public void AddContactToList(UserInfo contact)
        {
            ContactRow row = new ContactRow(contact.id);

            row.ContextMenu = BuildContactMenu(contact);

            row.DoubleTapped += TxtContact_PreviewMouseDoubleClick;
            row.PointerPressed += txtContact_PreviewMouseDown;
            row.PointerEntered += txtContact_MouseEnter;
            row.PointerExited += txtContact_MouseLeave;

            row.Update(contact);
            row.ApplyTheme();

            listContacts.Add(row);

            RebuildContactList();
        }

        /// <summary>Builds the right click menu for a contact row.</summary>
        private ContextMenu BuildContactMenu(UserInfo contact)
        {
            ContextMenu context = new ContextMenu();

            MenuItem openChatItem = new MenuItem { Tag = contact.id, Header = Language.Get("contact.sendmessage") };
            MenuItem favouriteItem = new MenuItem { Tag = contact.id };
            MenuItem blockItem = new MenuItem { Tag = contact.id };
            MenuItem deleteItem = new MenuItem { Tag = contact.id, Header = Language.Get("contact.remove") };

            favouriteItem.Header = Config.Favourites.IsFavourite(contact.id)
                ? Language.Get("contact.removefavourite")
                : Language.Get("contact.addfavourite");

            // Read from the contact rather than the menu's own wording, which changes with language.
            blockItem.Header = contact.blocked
                ? Language.Get("contact.unblock")
                : Language.Get("contact.block");

            openChatItem.Click += OpenChatItem_Click;
            favouriteItem.Click += FavouriteItem_Click;
            blockItem.Click += BlockItem_Click;
            deleteItem.Click += DeleteItem_Click;

            context.Items.Add(openChatItem);
            context.Items.Add(new Separator());
            context.Items.Add(favouriteItem);
            context.Items.Add(new Separator());
            context.Items.Add(blockItem);
            context.Items.Add(deleteItem);

            return context;
        }

        /// <summary>
        /// Rebuilds the list: favourites first, then everyone else, each group sorted with the
        /// contacts who are online at the top.
        /// </summary>
        private void RebuildContactList()
        {
            if (contactListView == null)
            {
                return;
            }

            List<ContactRow> favourites = new List<ContactRow>();
            List<ContactRow> others = new List<ContactRow>();

            foreach (ContactRow row in listContacts)
            {
                if (Config.Favourites.IsFavourite(row.ContactId))
                {
                    favourites.Add(row);
                }
                else
                {
                    others.Add(row);
                }
            }

            Sort(favourites);
            Sort(others);

            contactListView.Items.Clear();

            // The headings only earn their place once the list is actually split into groups.
            bool showHeaders = favourites.Count > 0;

            if (showHeaders)
            {
                favouritesHeader.SetText(Language.Format("main.group.favourites",
                    CountOnline(favourites), favourites.Count));

                contactListView.Items.Add(favouritesHeader);

                if (favouritesHeader.IsExpanded)
                {
                    foreach (ContactRow row in favourites)
                    {
                        contactListView.Items.Add(row);
                    }
                }
            }

            if (others.Count > 0)
            {
                if (showHeaders)
                {
                    contactsHeader.SetText(Language.Format("main.group.contacts",
                        CountOnline(others), others.Count));

                    contactListView.Items.Add(contactsHeader);
                }

                if (!showHeaders || contactsHeader.IsExpanded)
                {
                    foreach (ContactRow row in others)
                    {
                        contactListView.Items.Add(row);
                    }
                }
            }

            ResizeContactRows();
            UpdateContactCount();
        }

        /// <summary>Online contacts first, then by name, matching how the original ordered them.</summary>
        private static void Sort(List<ContactRow> rows)
        {
            rows.Sort((left, right) =>
            {
                UserInfo leftUser = Find(left.ContactId);
                UserInfo rightUser = Find(right.ContactId);

                bool leftOnline = leftUser != null && leftUser.status != (int)UserStatus.Offline;
                bool rightOnline = rightUser != null && rightUser.status != (int)UserStatus.Offline;

                if (leftOnline != rightOnline)
                {
                    return leftOnline ? -1 : 1;
                }

                string leftName = leftUser == null ? left.ContactId : leftUser.name;
                string rightName = rightUser == null ? right.ContactId : rightUser.name;

                return string.Compare(leftName, rightName, StringComparison.CurrentCultureIgnoreCase);
            });
        }

        private static int CountOnline(List<ContactRow> rows)
        {
            int count = 0;

            foreach (ContactRow row in rows)
            {
                UserInfo user = Find(row.ContactId);

                if (user != null && user.status != (int)UserStatus.Offline)
                {
                    count++;
                }
            }

            return count;
        }

        private static UserInfo Find(string contactId)
        {
            if (Personal.USER_CONTACTS == null || contactId == null)
            {
                return null;
            }

            return Personal.USER_CONTACTS.FirstOrDefault(
                x => string.Equals(x.id.Trim(), contactId.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private ContactRow FindRow(string contactId)
        {
            return listContacts.FirstOrDefault(
                x => string.Equals(x.ContactId.Trim(), (contactId ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Refreshes a row's second line when a new message arrives for that contact.</summary>
        private void OnConversationChanged(string contactId)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                ContactRow row = FindRow(contactId);

                if (row != null)
                {
                    row.RefreshSecondLine(Find(contactId));
                }
            });
        }

        private void FavouriteItem_Click(object sender, RoutedEventArgs e)
        {
            string contactId = ((MenuItem)sender).Tag.ToString();

            Config.Favourites.Toggle(contactId);

            // The menu wording flips with the new state.
            ContactRow row = FindRow(contactId);
            UserInfo user = Find(contactId);

            if (row != null && user != null)
            {
                row.ContextMenu = BuildContactMenu(user);
            }
        }

        #endregion

        private void TxtContact_PreviewMouseDoubleClick(object sender, TappedEventArgs e)
        {
            foreach (UserInfo contact in Personal.USER_CONTACTS)
            {
                if (contact.id.ToString() == ((ContactRow)sender).ContactId)
                {
                    OpenChatWindow(contact);

                    e.Handled = true;

                    break;
                }
            }
        }

        public void OpenChatWindow(UserInfo userInfo)
        {
            Windows.ChatWindow chatWindow = ManageChatWindows.GetChatWindow(userInfo.id);
            if (chatWindow != null)
            {
                chatWindow.Activate();
                chatWindow.Focus();
            }
        }

        private void OpenChatItem_Click(object sender, RoutedEventArgs e)
        {
            Windows.ChatWindow chatWindow = ManageChatWindows.GetChatWindow(((MenuItem)sender).Tag.ToString());
            if (chatWindow != null)
            {
                chatWindow.Activate();
                chatWindow.Focus();
            }
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            Client.DeleteContact(((MenuItem)sender).Tag.ToString());
        }

        public void RemoveContact(string userID)
        {
            lock (listContacts)
            {
                ContactRow row = FindRow(userID);

                if (row == null)
                {
                    return;
                }

                ManageChatWindows.RemoveChatWindow(userID);

                listContacts.Remove(row);

                UserInfo user = Find(userID);

                if (user != null)
                {
                    lock (Personal.USER_CONTACTS)
                    {
                        Personal.USER_CONTACTS.Remove(user);
                    }
                }
            }

            RebuildContactList();
        }

        private void BlockItem_Click(object sender, RoutedEventArgs e)
        {
            Client.BlockContact(((MenuItem)sender).Tag.ToString());
        }

        void txtContact_MouseLeave(object sender, PointerEventArgs e)
        {
            ContactRow row = (ContactRow)sender;

            if (row.IsFocused == false)
            {
                row.BorderThickness = new Thickness(0);
                row.Background = Brushes.Transparent;
            }
        }

        void txtContact_MouseEnter(object sender, PointerEventArgs e)
        {
            ContactRow row = (ContactRow)sender;

            if (row.IsFocused == false)
            {
                row.Background = BrushHelper.VerticalGradient(
                    Config.Theme.RowHoverFrom, Config.Theme.RowHoverTo);
                row.BorderThickness = new Thickness(1, 1, 1, 1);
                row.BorderBrush = Config.Theme.RowHoverBorder;
            }
        }

        void txtContact_PreviewMouseDown(object sender, PointerPressedEventArgs e)
        {
            foreach (ContactRow contact in listContacts)
            {
                contact.BorderThickness = new Thickness(0);
                contact.Background = Brushes.Transparent;
            }

            ContactRow row = (ContactRow)sender;

            row.Background = BrushHelper.VerticalGradient(
                Config.Theme.RowSelectedFrom, Config.Theme.RowSelectedTo);
            row.BorderThickness = new Thickness(1, 1, 1, 1);
            row.BorderBrush = Config.Theme.RowSelectedBorder;
        }

        public void UpdateContact(UserInfo userInfo)
        {
            bool needsRebuild;

            lock (Personal.USER_CONTACTS)
            {
                UserInfo userFound = Personal.USER_CONTACTS.FirstOrDefault(p => p.id == userInfo.id.Trim());

                if (userFound == null)
                {
                    Personal.USER_CONTACTS.Add(userInfo);
                    AddContactToList(userInfo);

                    return;
                }

                bool userJustLoggedOn = false;
                bool userJustLoggedOff = false;

                ManageChatWindows.UpdateChatWindowUser(userInfo);

                if (userFound.status != userInfo.status & userFound.status == Convert.ToInt16(UserStatus.Offline)
                    & userInfo.status != Convert.ToInt16(UserStatus.Offline))
                {
                    userJustLoggedOn = true;
                }

                if (userFound.status != userInfo.status & userFound.status != Convert.ToInt16(UserStatus.Offline)
                    & userInfo.status == Convert.ToInt16(UserStatus.Offline))
                {
                    userJustLoggedOff = true;
                }

                bool nameChanged = userFound.name != userInfo.name;

                userFound.name = userInfo.name;
                userFound.status = userInfo.status;
                userFound.avatar = userInfo.avatar;
                userFound.comment = userInfo.comment;
                userFound.blocked = userInfo.blocked;

                ContactRow row = FindRow(userInfo.id);

                if (row != null)
                {
                    row.Update(userFound);
                    row.ContextMenu = BuildContactMenu(userFound);
                }

                // Only a change that affects ordering or the headings needs a full rebuild.
                needsRebuild = userJustLoggedOn || userJustLoggedOff || nameChanged;

                if (userJustLoggedOn & Personal.USER_INFO.status == Convert.ToInt16(UserStatus.Available) & !userInfo.blocked)
                {
                    Notification.NotificationManager.Showpopup(userInfo.name,
                        Language.Get("notification.signedin"), null);

                    Resource.Sounds.Player.PlaySound(Resource.Sounds.Identifiers.ONLINE);
                }
            }

            if (needsRebuild)
            {
                RebuildContactList();
            }
            else
            {
                UpdateContactCount();
            }
        }

        public void UpdateContactCount()
        {
            int countOnlineContacts = 0;

            foreach (UserInfo contact in Personal.USER_CONTACTS)
            {
                if ((UserStatus)contact.status != UserStatus.Offline)
                {
                    countOnlineContacts++;
                }
            }

            txtFriends.Text = Language.Format("main.friends", countOnlineContacts, Personal.USER_CONTACTS.Count);
        }

        private void btnAddFriend_Click(object sender, RoutedEventArgs e)
        {
            FrmAddNewFriend frmAddNewFriend = new FrmAddNewFriend();

            frmAddNewFriend.ShowDialog(() => borderAddFriend.BorderThickness = new Thickness(0));
        }

        private void btnAddFriend_MouseEnter(object sender, PointerEventArgs e)
        {
            borderAddFriend.BorderThickness = new Thickness(1);
            borderAddFriend.BorderBrush = new BrushConverter().ConvertFrom("#D6D6D6");
        }

        private void btnAddFriend_MouseLeave(object sender, PointerEventArgs e)
        {
            borderAddFriend.BorderThickness = new Thickness(0);
        }

        private void menuItemArrowBusy_Click(object sender, RoutedEventArgs e)
        {
            Personal.USER_INFO.status = Convert.ToInt16(UserStatus.Busy);
            Client.SendUserUpdate();
        }

        private void menuItemArrowAvailable_Click(object sender, RoutedEventArgs e)
        {
            Personal.USER_INFO.status = Convert.ToInt16(UserStatus.Available);
            Client.SendUserUpdate();
        }

        private void menuItemArrowAway_Click(object sender, RoutedEventArgs e)
        {
            Personal.USER_INFO.status = Convert.ToInt16(UserStatus.Away);
            Client.SendUserUpdate();
        }

        private void menuItemArrowOffline_Click(object sender, RoutedEventArgs e)
        {
            Personal.USER_INFO.status = Convert.ToInt16(UserStatus.Offline);
            Client.SendUserUpdate();
        }

        private void menuItemArrowOptions_Click(object sender, RoutedEventArgs e)
        {
            FrmOptions frmOptions = new FrmOptions();

            frmOptions.ShowDialog(null);
        }

        private void menuItemArrowExit_Click(object sender, RoutedEventArgs e)
        {
            ManageChatWindows.CloseAllOpenChatWindows();

            Environment.Exit(0);
        }

        private void txtQuickMessage_GotKeyboardFocus(object sender, GotFocusEventArgs e)
        {
            if (!isCommentBeingEdited)
            {
                Keyboard.ClearFocus(this);
            }
        }
    }
}
