using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using WLMServer.Network;

using WLMData.Enums;
using WLMData.Data.Packets;
using WLMServer.Network.UserData;

namespace WLMServer.Network.PacketHandling
{
    /// <summary>
    /// Passes game invitations and moves between two users. The game itself is played by the two
    /// clients, which both check every move, so the server keeps no state for it.
    /// </summary>
    class TransferGame : PacketHandler
    {
        public TransferGame(Server server) : base(server) { }

        public override void InitializePacket()
        {
            NetworkComms.AppendGlobalIncomingPacketHandler<GameSignal>(
                PacketName.sendGameSignal.ToString(), Transfer);
        }

        protected void Transfer(PacketHeader header, Connection connection, GameSignal signal)
        {
            ConnectedUser senderUser = server.GetConnectedUser(connection);

            if (senderUser == null || signal == null || string.IsNullOrWhiteSpace(signal.id))
            {
                return;
            }

            string to = signal.id.Trim();

            Connection targetConnection = server.GetConnectionFromUserID(to);

            if (targetConnection == null)
            {
                // Only worth answering an invitation; anything later is moot once they have gone.
                if ((GameSignalType)signal.signal == GameSignalType.invite)
                {
                    server.SendPacket(connection, PacketName.sendGameSignal.ToString(),
                        new GameSignal(to, (int)GameSignalType.unavailable, signal.game, 0));
                }

                return;
            }

            server.SendPacket(targetConnection, PacketName.sendGameSignal.ToString(),
                new GameSignal(senderUser.user.id, signal.signal, signal.game, signal.value));
        }
    }
}
