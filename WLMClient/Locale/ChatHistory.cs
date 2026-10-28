using System;
using System.Collections.Generic;

namespace WLMClient.Locale
{
    /// <summary>
    /// Everything shown in each conversation this session, so a conversation window that is closed
    /// and opened again - by hand, or because a new message arrived - carries on from where it
    /// was instead of starting empty. Cleared on sign out; nothing is written to disk.
    /// </summary>
    static class ChatHistory
    {
        public enum Kind
        {
            Message,

            /// <summary>A line such as a nudge or a game invitation.</summary>
            Notice,

            Attachment
        }

        public sealed class Entry
        {
            public Kind Kind;
            public DateTime Time;

            /// <summary>Display name of the sender, for messages and attachments.</summary>
            public string From;

            /// <summary>The message, the notice, or the attachment's file name.</summary>
            public string Text;

            /// <summary>Where an attachment was saved or sent from.</summary>
            public string Path;
            public long Size;
        }

        /// <summary>Per conversation; the oldest lines go first once a conversation runs longer.</summary>
        private const int MaximumEntries = 1000;

        private static readonly Dictionary<string, List<Entry>> conversations =
            new Dictionary<string, List<Entry>>(StringComparer.OrdinalIgnoreCase);

        private static readonly object locker = new object();

        public static void Add(string contactId, Entry entry)
        {
            if (string.IsNullOrWhiteSpace(contactId) || entry == null)
            {
                return;
            }

            lock (locker)
            {
                List<Entry> entries;

                if (!conversations.TryGetValue(contactId.Trim(), out entries))
                {
                    entries = new List<Entry>();
                    conversations[contactId.Trim()] = entries;
                }

                entries.Add(entry);

                if (entries.Count > MaximumEntries)
                {
                    entries.RemoveRange(0, entries.Count - MaximumEntries);
                }
            }
        }

        /// <summary>A copy of the conversation with a contact so far, oldest first.</summary>
        public static List<Entry> Get(string contactId)
        {
            if (string.IsNullOrWhiteSpace(contactId))
            {
                return new List<Entry>();
            }

            lock (locker)
            {
                List<Entry> entries;

                return conversations.TryGetValue(contactId.Trim(), out entries)
                    ? new List<Entry>(entries)
                    : new List<Entry>();
            }
        }

        /// <summary>Forgets every conversation, for signing out.</summary>
        public static void Clear()
        {
            lock (locker)
            {
                conversations.Clear();
            }
        }
    }
}
