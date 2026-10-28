using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Net;
using System.Threading.Tasks;

using Avalonia.Threading;

using WLMClient.Compat;

using WLMData;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Tools;
using NetworkCommsDotNet.DPSBase;
using NetworkCommsDotNet.Connections;
using NetworkCommsDotNet.Connections.TCP;
using WLMData.Data.Packets;
using WLMData.Enums;
using WLMClient.Network.PacketHandling;
using WLMClient.UI.Windows;
using WLMClient.Locale;

namespace WLMClient.Network
{
    public class Client
    {
        public static string version = "1.0.0";
        public static ConnectionInfo connectionInfo { get; set; }

        private static readonly object serverInfoLocker = new object();
        private static Action<ServerInfo> pendingServerInfo;

        private static PacketHandler connectionedClosed, authentication, receiveContact, receiveMessage, receiveNudge,
            receiveContactDelete, receiveWritingStatus, receiveFriendRequest, personalUserUpdate, receiveFile, receiveUsernameChange, receiveGameSignal, receiveServerInfo;

        public static void Load(MainWindow mainWindow)
        {
            authentication = new Authentication(mainWindow);
            receiveContact = new ReceiveContact(mainWindow);
            receiveMessage = new ReceiveMessage(mainWindow);
            receiveNudge = new ReceiveNudge(mainWindow);
            receiveFriendRequest = new ReceiveFriendRequest(mainWindow);
            personalUserUpdate = new PersonalUserUpdate(mainWindow);
            receiveContactDelete = new ReceiveContactDelete(mainWindow);
            connectionedClosed = new ConnectionClosed(mainWindow);
            receiveWritingStatus = new ReceiveWritingStatus(mainWindow);
            receiveFile = new ReceiveFile(mainWindow);
            receiveUsernameChange = new ReceiveUsernameChange(mainWindow);
            receiveGameSignal = new ReceiveGameSignal(mainWindow);
            receiveServerInfo = new ReceiveServerInfo(mainWindow);

            Personal.USER_CONTACTS = new List<UserInfo>();
            Personal.USER_INFO = null;
            Personal.OPEN_CHAT_WINDOWS = new List<ChatWindow>();

            NetworkComms.DefaultSendReceiveOptions = new SendReceiveOptions(DPSManager.GetDataSerializer<ProtobufSerializer>(),
                NetworkComms.DefaultSendReceiveOptions.DataProcessors, NetworkComms.DefaultSendReceiveOptions.Options);

            RijndaelPSKEncrypter.AddPasswordToOptions(NetworkComms.DefaultSendReceiveOptions.Options, Config.Properties.SERVER_ENCRYPTION_KEY);
            NetworkComms.DefaultSendReceiveOptions.DataProcessors.Add(DPSManager.GetDataProcessor<RijndaelPSKEncrypter>());
        }

        /// <summary>
        /// Looks up the configured server and opens the connection on a background thread, then
        /// calls <paramref name="finished"/> on the UI thread with whether it worked, having
        /// already told the user why if it did not.
        ///
        /// Both steps used to run on the UI thread when Sign in was clicked. A server behind a
        /// firewall that drops packets gives no answer at all, so the window froze until the
        /// operating system gave up a minute or more later.
        /// </summary>
        public static void ConnectInBackground(Action<bool> finished)
        {
            ((ConnectionClosed)connectionedClosed).Close();

            NetworkComms.Shutdown();

            ((ConnectionClosed)connectionedClosed).Open();

            connectionInfo = null;

            string address = Config.Properties.SERVER_ADDRESS;
            int port = Config.Properties.SERVER_PORT;

            Task.Run(() =>
            {
                ConnectionInfo info = null;
                bool resolved = false;

                try
                {
                    info = new ConnectionInfo(address, port);
                    resolved = true;

                    TCPConnection.GetConnection(info);
                }
                catch
                {
                    info = null;
                }

                Dispatcher.UIThread.Post(() =>
                {
                    if (info == null)
                    {
                        if (!resolved)
                        {
                            MessageBox.Show(Locale.Language.Format("error.server.text", address),
                                Locale.Language.Get("error.server.title"), MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        else
                        {
                            MessageBox.Show(
                                Locale.Language.Format("error.unreachable.text",
                                    Config.Properties.FormatServer(address, port), port),
                                Locale.Language.Get("error.unreachable.title"), MessageBoxButton.OK, MessageBoxImage.Error);
                        }

                        finished(false);

                        return;
                    }

                    connectionInfo = info;

                    finished(true);
                });
            });
        }

        public static void SendPacket(string packetType, object packetData)
        {
            if (connectionInfo == null)
            {
                return;
            }

            try
            {
                TCPConnection.GetConnection(connectionInfo).SendObject(packetType, packetData);
            }
            catch
            {
                NetworkComms.Shutdown();
            }
        }

        public static void AuthenticateUser(string userID, string password, int status)
        {
            LoginRequest loginRequest = new LoginRequest(userID, password, status, version);

            SendPacket(PacketName.requestLogin.ToString(), loginRequest);
        }

        /// <summary>
        /// Asks the server what it will say to somebody who has not signed in yet. The sign in page
        /// uses this for the "Sign up." link, so it points wherever that particular server puts it.
        /// The callback runs on the receive thread, and is dropped if the answer never comes.
        /// </summary>
        public static void RequestServerInfo(Action<ServerInfo> onAnswer)
        {
            lock (serverInfoLocker)
            {
                pendingServerInfo = onAnswer;
            }

            SendPacket(PacketName.requestServerInfo.ToString(), "");
        }

        /// <summary>Delivers a server's answer to the one request waiting for it, at most once.</summary>
        public static void CompleteServerInfo(ServerInfo info)
        {
            Action<ServerInfo> waiting;

            lock (serverInfoLocker)
            {
                waiting = pendingServerInfo;
                pendingServerInfo = null;
            }

            if (waiting != null)
            {
                waiting(info);
            }
        }

        public static void AddNewContact(string userID)
        {
            SendPacket(PacketName.requestNewContact.ToString(), userID);
        }

        public static void SendFriendRequestResponse(string userID, FriendRequestResponseCode responseCode)
        {
            SendPacket(PacketName.sendFriendRequestResponse.ToString(), new FriendRequestResponse(userID, (int)responseCode));
        }

        public static void SendUserUpdate()
        {
            SendPacket(PacketName.sendUserUpdate.ToString(), Personal.USER_INFO);
        }

        public static void BlockContact(string userID)
        {
            SendPacket(PacketName.sendContactBlock.ToString(), userID);
        }

        public static void DeleteContact(string userID)
        {
            SendPacket(PacketName.sendContactDelete.ToString(), userID);
        }

        public static void SendMessage(Message message)
        {
            SendPacket(PacketName.sendMessage.ToString(), message);
        }

        public static void SendNudge(string userID)
        {
            SendPacket(PacketName.sendNudge.ToString(), userID);
        }

        public static void SendFile(string userID, string fileName, byte[] data)
        {
            SendPacket(PacketName.sendFileTransfer.ToString(), new FileTransfer(userID, fileName, data));
        }

        /// <summary>Sends one step of a game played in a conversation to a contact.</summary>
        public static void SendGameSignal(string userID, GameSignalType signal, GameKind game, int value)
        {
            SendPacket(PacketName.sendGameSignal.ToString(), new GameSignal(userID, (int)signal, (int)game, value));
        }

        /// <summary>Asks the server to change the name this account signs in with.</summary>
        public static void RequestUsernameChange(string newUsername)
        {
            SendPacket(PacketName.requestUsernameChange.ToString(), newUsername);
        }

        public static void SendWritingStatus(string userID, bool isWriting)
        {
            SendPacket(PacketName.sendWritingStatus.ToString(), new WritingStatus(userID, isWriting));
        }
    }
}
