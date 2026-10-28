using System;
using System.Collections.Generic;

namespace WLMClient.Locale
{
    /// <summary>
    /// The most recent message exchanged with each contact, used for the contact list's second
    /// line when a contact has no personal message set.
    ///
    /// This only covers the current session. The conversations themselves are in
    /// <see cref="ChatHistory"/>.
    /// </summary>
    static class Conversations
    {
        private sealed class Entry
        {
            public string From;
            public string Text;
        }

        private static readonly Dictionary<string, Entry> lastMessages =
            new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        private static readonly object locker = new object();

        /// <summary>Raised when a conversation's latest message changes.</summary>
        public static event Action<string> Changed;

        /// <summary>
        /// Records the latest message for a contact.
        /// </summary>
        /// <param name="contactId">The other party in the conversation.</param>
        /// <param name="from">Display name of whoever sent it.</param>
        /// <param name="text">The message body.</param>
        public static void Record(string contactId, string from, string text)
        {
            if (string.IsNullOrWhiteSpace(contactId))
            {
                return;
            }

            lock (locker)
            {
                lastMessages[contactId.Trim()] = new Entry { From = from ?? "", Text = Flatten(text) };
            }

            Action<string> handler = Changed;

            if (handler != null)
            {
                handler(contactId.Trim());
            }
        }

        /// <summary>
        /// The latest message for a contact rendered as "Name: message", or null if there has not
        /// been one this session.
        /// </summary>
        public static string GetSummary(string contactId)
        {
            if (string.IsNullOrWhiteSpace(contactId))
            {
                return null;
            }

            lock (locker)
            {
                Entry entry;

                if (!lastMessages.TryGetValue(contactId.Trim(), out entry))
                {
                    return null;
                }

                if (string.IsNullOrEmpty(entry.Text))
                {
                    return null;
                }

                return string.IsNullOrEmpty(entry.From) ? entry.Text : entry.From + ": " + entry.Text;
            }
        }

        public static void Clear()
        {
            lock (locker)
            {
                lastMessages.Clear();
            }
        }

        /// <summary>Collapses a message to a single line so it fits on one row.</summary>
        private static string Flatten(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            text = text.Replace("\r", " ").Replace("\n", " ").Trim();

            while (text.Contains("  "))
            {
                text = text.Replace("  ", " ");
            }

            return text;
        }
    }
}
