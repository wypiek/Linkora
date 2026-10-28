using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Avalonia.Threading;

using WLMClient.Compat;

using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;

using WLMClient.UI.Windows;
using WLMData.Enums;
using WLMData.Data.Packets;
using WLMClient.Locale;

namespace WLMClient.Network.PacketHandling
{
    /// <summary>Handles game invitations and moves coming from a contact.</summary>
    class ReceiveGameSignal : PacketHandler
    {
        public ReceiveGameSignal(MainWindow mainWindow) : base(mainWindow) { }

        public override void InitializePacket()
        {
            NetworkComms.AppendGlobalIncomingPacketHandler<GameSignal>(
                PacketName.sendGameSignal.ToString(), Signal);
        }

        protected void Signal(PacketHeader header, Connection connection, GameSignal gameSignal)
        {
            PostLoginAction(gameSignal);
        }

        public override void PostLoginAction(object packet)
        {
            base.PostLoginAction(packet);

            mainWindow.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() =>
            {
                ManageChatWindows.ReceiveGameSignal((GameSignal)packet);
            }));
        }
    }
}
