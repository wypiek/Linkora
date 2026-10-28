using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WLMData.Enums
{
    /// <summary>The steps of a game played inside a conversation.</summary>
    public enum GameSignalType
    {
        /// <summary>Asking the other party to play.</summary>
        invite = 0,

        /// <summary>The invitation was taken up; the inviter makes the first move.</summary>
        accept = 1,

        /// <summary>The invitation was turned down, or withdrawn before it was answered.</summary>
        decline = 2,

        /// <summary>A move; the packet's value says which one.</summary>
        move = 3,

        /// <summary>The sender closed the game.</summary>
        leave = 4,

        /// <summary>The other party is not signed in.</summary>
        unavailable = 5
    }
}
