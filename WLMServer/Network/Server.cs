using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net;
using NetworkCommsDotNet;
using NetworkCommsDotNet.DPSBase;
using NetworkCommsDotNet.Connections;
using NetworkCommsDotNet.Tools;
using NetworkCommsDotNet.Connections.TCP;

using WLMData.Enums;
using WLMData.Data;
using WLMData.Data.Packets;
using WLMServer.Network.UserData;
using WLMServer.Database;

using System.Threading;

namespace WLMServer.Network
{
    class Server
    {
        public string version = "1.0.0";
        private Dictionary<Connection, ConnectedUser> users;
        private PacketHandler authentication, addNewContact, addNewContactResponse, personalUserUpdate,
            deleteAndBlockContact, transferMessage, transferNudge, transferWritingStatus, transferFile,
            changeUsername, transferGame, serverInformation;
        private WebServer webServer;
        public AccountManager accountManager;

        public Server()
        {
            users = new Dictionary<Connection, ConnectedUser>();

            authentication = new PacketHandling.Authentication(this);
            addNewContact = new PacketHandling.AddNewContact(this);
            addNewContactResponse = new PacketHandling.AddNewContactResponse(this);
            personalUserUpdate = new PacketHandling.PersonalUserUpdate(this);
            deleteAndBlockContact = new PacketHandling.DeleteAndBlockContact(this);
            transferMessage = new PacketHandling.TransferMessage(this);
            transferNudge = new PacketHandling.TransferNudge(this);
            transferWritingStatus = new PacketHandling.TransferWritingStatus(this);
            transferFile = new PacketHandling.TransferFile(this);
            changeUsername = new PacketHandling.ChangeUsername(this);
            transferGame = new PacketHandling.TransferGame(this);
            serverInformation = new PacketHandling.ServerInformation(this);

            accountManager = new AccountManager();

            NetworkComms.AppendGlobalConnectionCloseHandler(HandleConnectionClosed);

            NetworkComms.DefaultSendReceiveOptions = new SendReceiveOptions(DPSManager.GetDataSerializer<ProtobufSerializer>(), NetworkComms.DefaultSendReceiveOptions.DataProcessors, NetworkComms.DefaultSendReceiveOptions.Options);
            if (!NetworkComms.DefaultSendReceiveOptions.DataProcessors.Contains(DPSManager.GetDataProcessor<RijndaelPSKEncrypter>()))
            {
                RijndaelPSKEncrypter.AddPasswordToOptions(NetworkComms.DefaultSendReceiveOptions.Options, Config.Properties.SERVER_ENCRYPTION_KEY);
                NetworkComms.DefaultSendReceiveOptions.DataProcessors.Add(DPSManager.GetDataProcessor<RijndaelPSKEncrypter>());
            }

            NetworkComms.Shutdown();
            Connection.StartListening(ConnectionType.TCP, new IPEndPoint(IPAddress.Any, Config.Properties.SERVER_PORT));

            foreach (IPEndPoint listenEndPoint in Connection.ExistingLocalListenEndPoints(ConnectionType.TCP))
            {
                Program.WriteToConsole("Local End Point: " + listenEndPoint.Address + ":" + listenEndPoint.Port);
            }

            StartWebServer();

            BroadCastContacts(Config.Properties.BROADCAST_INTERVAL);
        }

        /// <summary>
        /// Starts the built-in website, which carries the sign up page and, when avatars are on,
        /// their upload and download endpoints. Disabled when http_port is 0, in which case sign up
        /// is the console's "register" command and avatars need the bundled upload.php elsewhere.
        /// </summary>
        private void StartWebServer()
        {
            bool wantAvatars = Config.Properties.AVATAR_ENABLE;

            // registration_url is only about what the client is told to open, which may well be a
            // proxy in front of this very page, so it has no say in whether the page runs.
            bool wantRegistration = Config.Properties.REGISTRATION_ENABLE;

            if (Config.Properties.HTTP_PORT <= 0 || (!wantAvatars && !wantRegistration))
            {
                return;
            }

            try
            {
                string storagePath = Config.Properties.AVATAR_STORAGE_PATH;

                if (!System.IO.Path.IsPathRooted(storagePath))
                {
                    storagePath = System.IO.Path.Combine(AppContext.BaseDirectory, storagePath);
                }

                webServer = new WebServer(Config.Properties.HTTP_PORT, storagePath,
                    wantAvatars, wantRegistration);
                webServer.Start();
            }
            catch (Exception exception)
            {
                Program.WriteToConsole("Could not start the website: " + exception.Message);
                Program.WriteToConsole("Sign up and avatars will not work until this is resolved.");
            }
        }

        public void BroadCastContacts(int intervalSeconds)
        {
            new Thread(delegate ()
            {
                while (true)
                {
                    Thread.Sleep(intervalSeconds * 1000);

                    lock (users)
                    {
                        foreach (KeyValuePair<Connection, ConnectedUser> user in users)
                        {
                            SendUsersContactList(user.Key, user.Value.user.id);
                        }
                    }
                }
            }).Start();
        }

        public void SendPacket(Connection connection, string packetType, object packetData)
        {
            try
            {
                TCPConnection.GetConnection(connection.ConnectionInfo).SendObject(packetType, packetData);
            }
            catch
            {
                HandleConnectionClosed(connection);
            }
        }

        private void HandleConnectionClosed(Connection connection)
        {
            lock (users)
            {
                if (users.ContainsKey(connection))
                {
                    UserInfo user = users[connection].user;
                    user.status = 0;
                    SendUpdateToUsersContactList(connection, user);

                    users.Remove(connection);
                }
            }
        }

        public ConnectedUser GetConnectedUser(Connection connection)
        {
            return users[connection];
        }

        public Connection GetConnectionFromUserID(string userID)
        {
            return users.FirstOrDefault(x => x.Value.user.id.Trim().ToLower() == userID.Trim().ToLower()).Key;
        }

        public void SendFriendRequestToUser(Connection connection, UserInfo userInfo)
        {
            SendPacket(connection, PacketName.sendFriendRequestToUser.ToString(), userInfo);
        }

        public void AddUserToConnectedUsersList(Connection connection, UserInfo userInfo)
        {
            users[connection] = new ConnectedUser(userInfo, new ContactList(accountManager.GetContacts(userInfo.id)));
        }

        public void SendPersonalUserUpdate(Connection connection, UserInfo userInfo)
        {
            SendPacket(connection, PacketName.sendPersonalUserUpdate.ToString(), userInfo);
        }

        public void SendDeleteContact(Connection connection, UserInfo userInfo)
        {
            SendPacket(connection, PacketName.sendContactDelete.ToString(), userInfo);
        }

        public void SendUsersContactList(Connection connection, string id)
        {
            ContactList contactList = users[connection].contactList;

            List<Contact> contacts = contactList.GetContacts();

            foreach (Contact contact in contacts)
            {
                Connection sendConnection = users.FirstOrDefault(x => x.Value.user.id.Trim() == contact.id.Trim()).Key;
                ConnectedUser sendUser = null;
                string sendUserName = "";
                int sendUserStatus = 0;
                bool sendUserIsBlocked = false;
                string sendUserComment = "";
                string sendUserAvatar = "";

                if (sendConnection == null)
                {
                    sendUser = new ConnectedUser(accountManager.GetUser(contact.id), new ContactList(contact.id.Trim()));

                    if (sendUser == null)
                    {
                        continue;
                    }
                }
                else
                {
                    sendUser = users[sendConnection];
                }

                sendUserStatus = sendUser.user.status;
                sendUserName = sendUser.user.name;
                sendUserComment = sendUser.user.comment;
                sendUserAvatar = sendUser.user.avatar;

                bool isBlocked = sendUser.contactList.IsUserBlocked(users[connection].user.id);
                bool isAvailable = contactList.IsUserAccepted(contact.id);

                if (!isAvailable)
                {
                    sendUserAvatar = "";
                    sendUserName = sendUser.user.id;
                    sendUserStatus = (int)UserStatus.Offline;
                    sendUserComment = "";
                }

                if (sendUser.user.status != (int)UserStatus.Offline)
                {
                    if (isBlocked)
                    {
                        sendUserStatus = (int)UserStatus.Offline;
                    }
                }

                if (contactList.IsUserBlocked(sendUser.user.id))
                {
                    sendUserIsBlocked = true;
                    sendUserName = "(BLOCKED) " + sendUserName;
                }
                
                SendPacket(connection, PacketName.sendContact.ToString(), new UserInfo(sendUser.user.id, sendUserName, sendUserComment,
                    sendUserStatus, sendUserAvatar, sendUserIsBlocked));
            }
        }

        public void SendUpdateToUsersContactList(Connection connection, UserInfo userInfo)
        {
            ContactList contactList = users[connection].contactList;
            List<Contact> contacts = contactList.GetContacts();

            foreach (Contact contact in contacts)
            {
                Connection contactConnection = users.FirstOrDefault(x => x.Value.user.id.Trim() == contact.id.Trim()).Key;

                if (contactConnection != null & contactConnection != connection)
                {
                    UserInfo sendUser = new UserInfo(userInfo.id, userInfo.name, userInfo.comment,
                        (int)userInfo.status, userInfo.avatar, false);

                    string sendUserName = sendUser.name;
                    bool sendUserIsBlocked = false;
                    int sendUserStatus = (int)sendUser.status;

                    if (!contactList.IsUserAccepted(contact.id))
                    {
                        continue;
                    }

                    if (users[contactConnection].contactList.IsUserBlocked(userInfo.id))
                    {
                        sendUserIsBlocked = true;
                        sendUserName = "(BLOCKED) " + sendUserName;
                    }

                    if (contactList.IsUserBlocked(contact.id))
                    {
                        sendUserStatus = (int)UserStatus.Offline;
                    }

                    SendPacket(contactConnection, PacketName.sendContact.ToString(), new UserInfo(sendUser.id, sendUserName, sendUser.comment,
                        sendUserStatus, sendUser.avatar, sendUserIsBlocked));
                }
            }
        }

        /// <summary>
        /// Moves a signed in user to a new name. The account row is already renamed by this point;
        /// this fixes up what is held in memory, including the contact lists of everyone currently
        /// online who has this user, which were loaded when they signed in.
        /// </summary>
        public void RenameUser(Connection connection, string oldId, string newId)
        {
            lock (users)
            {
                ConnectedUser renamed = users[connection];

                // UserInfo's id is read only, so the record is rebuilt around the new name.
                users[connection] = new ConnectedUser(
                    new UserInfo(newId, renamed.user.name, renamed.user.comment, renamed.user.status,
                        renamed.user.avatar, renamed.user.blocked),
                    renamed.contactList);

                foreach (KeyValuePair<Connection, ConnectedUser> user in users)
                {
                    if (user.Key == connection)
                    {
                        continue;
                    }

                    user.Value.contactList.RenameUser(oldId, newId);
                }
            }
        }

        public int GetUserCount()
        {
            return users.Count;
        }

        public void ConsoleEchoOnlineUsers()
        {
            foreach (KeyValuePair<Connection, ConnectedUser> user in users)
            {
                Program.WriteToConsole("User ID " + user.Value.user.id + " logged in from " + ((IPEndPoint)user.Key.ConnectionInfo.RemoteEndPoint).Address);
            }
        }

        public string ConsoleRegisterNewUser(string username, string password)
        {
            string returnValue = "";

            if (username.Length > 0 && password.Length > 0 && !accountManager.IsUserInDatabase(username))
            {
                accountManager.InsertNewAccount(username, password);

                returnValue = "RESULT: Account created successfully.";
            }
            else
            {
                returnValue = "RESULT: Account creation failed.";
            }
            
            return returnValue;
        }
    }
}
