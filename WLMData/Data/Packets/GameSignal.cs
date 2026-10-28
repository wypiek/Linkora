using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ProtoBuf;
using System.IO;
using NetworkCommsDotNet.DPSBase;

namespace WLMData.Data.Packets
{
    /// <summary>
    /// One step of a game played in a conversation. On the way to the server <see cref="id"/> is
    /// the other player; on the way back out it is whoever sent it. <see cref="value"/> carries a
    /// move, and is unused by the other signals.
    /// </summary>
    [ProtoContract]
    public class GameSignal : Serializer, IExplicitlySerialize
    {
        [ProtoMember(1)]
        public string id { get; private set; }

        [ProtoMember(2)]
        public int signal { get; private set; }

        [ProtoMember(3)]
        public int game { get; private set; }

        [ProtoMember(4)]
        public int value { get; private set; }

        private GameSignal() { }

        public GameSignal(string id, int signal, int game, int value)
        {
            this.id = id;
            this.signal = signal;
            this.game = game;
            this.value = value;
        }

        public void Serialize(Stream outputStream)
        {
            SerializeData(outputStream, id);
            SerializeData(outputStream, signal);
            SerializeData(outputStream, game);
            SerializeData(outputStream, value);
        }

        public void Deserialize(Stream inputStream)
        {
            id = DeserializeString(inputStream);
            signal = DeserializeInt(inputStream);
            game = DeserializeInt(inputStream);
            value = DeserializeInt(inputStream);
        }

        public static void Deserialize(Stream inputStream, out GameSignal gameSignal)
        {
            gameSignal = new GameSignal();
            gameSignal.Deserialize(inputStream);
        }
    }
}
