using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace WLMClient.Locale
{
    /// <summary>A language the interface can be shown in.</summary>
    class LanguageInfo
    {
        /// <summary>Culture code such as "en" or "pt-BR", taken from the file name.</summary>
        public string Code { get; set; }

        /// <summary>Name shown in the language list, written in that language.</summary>
        public string Name { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

    /// <summary>
    /// Interface text, loaded from plain "key = value" files in the Languages folder next to the
    /// executable. Anyone can add a language by dropping in another file; no rebuild is needed.
    ///
    /// English is compiled in as well, so the application still reads correctly if the files are
    /// missing, and so a translation that omits a key falls back to English rather than a blank.
    /// </summary>
    static class Language
    {
        public const string DefaultCode = "en";

        private static readonly Dictionary<string, string> current =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static readonly object locker = new object();

        private static List<LanguageInfo> available;

        /// <summary>Raised after the language changes so open windows can refresh their text.</summary>
        public static event Action Changed;

        public static string CurrentCode { get; private set; }

        /// <summary>The folder holding the translation files.</summary>
        public static string LanguagesDirectory
        {
            get { return Path.Combine(AppContext.BaseDirectory, "Languages"); }
        }

        #region Lookup

        /// <summary>Text for a key, falling back to English and then to the key itself.</summary>
        public static string Get(string key)
        {
            lock (locker)
            {
                string value;

                if (current.TryGetValue(key, out value) && !string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }

            string builtIn;

            return BuiltInEnglish.TryGetValue(key, out builtIn) ? builtIn : key;
        }

        /// <summary>Text for a key with {0}, {1}... placeholders filled in.</summary>
        public static string Format(string key, params object[] arguments)
        {
            string text = Get(key);

            try
            {
                return string.Format(text, arguments);
            }
            catch (FormatException)
            {
                // A translation with a malformed placeholder must not crash the interface.
                return text;
            }
        }

        /// <summary>The localised name of a presence status.</summary>
        public static string GetStatus(WLMData.Enums.UserStatus status)
        {
            return Get("status." + status.ToString().ToLowerInvariant());
        }

        #endregion

        #region Loading

        /// <summary>Every language that can be selected, English first and the rest by name.</summary>
        public static List<LanguageInfo> GetAvailable()
        {
            lock (locker)
            {
                if (available != null)
                {
                    return available;
                }

                Dictionary<string, LanguageInfo> found = new Dictionary<string, LanguageInfo>(
                    StringComparer.OrdinalIgnoreCase);

                found[DefaultCode] = new LanguageInfo { Code = DefaultCode, Name = "English" };

                try
                {
                    if (Directory.Exists(LanguagesDirectory))
                    {
                        foreach (string path in Directory.GetFiles(LanguagesDirectory, "*.lang"))
                        {
                            string code = Path.GetFileNameWithoutExtension(path);

                            if (string.IsNullOrWhiteSpace(code))
                            {
                                continue;
                            }

                            Dictionary<string, string> entries = ReadFile(path);

                            string name;

                            if (!entries.TryGetValue("language.name", out name) || string.IsNullOrWhiteSpace(name))
                            {
                                name = DescribeCode(code);
                            }

                            found[code] = new LanguageInfo { Code = code, Name = name };
                        }
                    }
                }
                catch
                {
                    // An unreadable folder just means only English is offered.
                }

                available = found.Values
                    .OrderBy(x => x.Code == DefaultCode ? 0 : 1)
                    .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                return available;
            }
        }

        /// <summary>
        /// Switches language. An unknown or empty code falls back to the system's language when one
        /// matches, and to English otherwise.
        /// </summary>
        public static void Load(string code)
        {
            List<LanguageInfo> languages = GetAvailable();

            if (string.IsNullOrWhiteSpace(code))
            {
                code = FindSystemLanguage(languages);
            }

            if (!languages.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)))
            {
                code = DefaultCode;
            }

            lock (locker)
            {
                current.Clear();
                CurrentCode = code;

                if (!string.Equals(code, DefaultCode, StringComparison.OrdinalIgnoreCase))
                {
                    string path = Path.Combine(LanguagesDirectory, code + ".lang");

                    foreach (KeyValuePair<string, string> entry in ReadFile(path))
                    {
                        current[entry.Key] = entry.Value;
                    }
                }
            }

            Action handler = Changed;

            if (handler != null)
            {
                handler();
            }
        }

        /// <summary>Picks the closest available match for the operating system's language.</summary>
        private static string FindSystemLanguage(List<LanguageInfo> languages)
        {
            try
            {
                string culture = CultureInfo.CurrentUICulture.Name;

                LanguageInfo exact = languages.FirstOrDefault(
                    x => string.Equals(x.Code, culture, StringComparison.OrdinalIgnoreCase));

                if (exact != null)
                {
                    return exact.Code;
                }

                string twoLetter = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

                LanguageInfo partial = languages.FirstOrDefault(
                    x => string.Equals(x.Code, twoLetter, StringComparison.OrdinalIgnoreCase));

                if (partial != null)
                {
                    return partial.Code;
                }
            }
            catch
            {
                // Fall through to English.
            }

            return DefaultCode;
        }

        private static string DescribeCode(string code)
        {
            try
            {
                return CultureInfo.GetCultureInfo(code).NativeName;
            }
            catch
            {
                return code;
            }
        }

        /// <summary>
        /// Reads a translation file. Lines are "key = value"; blank lines and lines starting with
        /// # are ignored, and \n in a value becomes a line break.
        /// </summary>
        private static Dictionary<string, string> ReadFile(string path)
        {
            Dictionary<string, string> entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (!File.Exists(path))
                {
                    return entries;
                }

                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string trimmed = line.Trim();

                    if (trimmed.Length == 0 || trimmed.StartsWith("#"))
                    {
                        continue;
                    }

                    int separator = trimmed.IndexOf('=');

                    if (separator <= 0)
                    {
                        continue;
                    }

                    string key = trimmed.Substring(0, separator).Trim();
                    string value = trimmed.Substring(separator + 1).Trim().Replace("\\n", "\n");

                    if (key.Length > 0)
                    {
                        entries[key] = value;
                    }
                }
            }
            catch
            {
                // A damaged file degrades to English rather than failing.
            }

            return entries;
        }

        #endregion

        /// <summary>
        /// The English text, compiled in so the application is never left without wording and so
        /// incomplete translations fall back key by key.
        /// </summary>
        private static readonly Dictionary<string, string> BuiltInEnglish =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "language.name", "English" },

            { "status.available", "Available" },
            { "status.busy", "Busy" },
            { "status.away", "Away" },
            { "status.offline", "Offline" },

            { "login.title", "Sign in" },
            { "login.prompt", "Sign in with your Linkora ID. Don't have one?" },
            { "login.signup", "Sign up." },
            { "login.server.watermark", "Server address" },
            { "login.server.tooltip", "Address of the Linkora server to sign in to, for example 127.0.0.1 or chat.example.com:1323" },
            { "login.rememberme", "Remember me" },
            { "login.rememberpassword", "Remember my password" },
            { "login.autosignin", "Sign me in automatically" },
            { "login.button", "Sign in" },
            { "login.signingin", "Signing in\u2026" },
            { "login.server.needed.title", "Server address needed" },
            { "login.server.needed.text", "Enter the address of the Linkora server you want to sign in to.\n\nFor example 127.0.0.1, chat.example.com, or chat.example.com:1323." },
            { "login.signup.needed.text", "Enter the address of the server you want to sign up on first." },
            { "login.signup.asking", "Sign up\u2026" },
            { "login.signup.unavailable.title", "No sign up page" },
            { "login.signup.unavailable.text", "{0} does not offer a sign up page. Ask whoever runs it for an account." },

            { "main.quickmessage", "Share a quick message" },
            { "main.friends", "Friends ({0}/{1})" },
            { "main.group.favourites", "Favourites ({0}/{1})" },
            { "main.group.contacts", "Contacts ({0}/{1})" },
            { "main.connectedto", "Connected to {0}" },
            { "main.addfriend", "Add a friend..." },
            { "main.connectedto.tooltip", "The Linkora server you are signed in to" },
            { "main.version", "Linkora {0}" },

            { "menu.available", "Available" },
            { "menu.busy", "Busy" },
            { "menu.away", "Away" },
            { "menu.appearoffline", "Appear Offline" },
            { "menu.options", "Options" },
            { "menu.exit", "Exit Messenger" },

            { "contact.sendmessage", "Send Message" },
            { "contact.block", "Block" },
            { "contact.unblock", "Unblock" },
            { "contact.remove", "Remove Contact" },
            { "contact.addfavourite", "Add to Favourites" },
            { "contact.removefavourite", "Remove from Favourites" },
            { "contact.blockedprefix", "(BLOCKED)" },

            { "chat.says", "{0} says" },
            { "chat.lastmessage", "Last message received at {0} on {1}." },
            { "chat.writing", "{0} is writing a message." },
            { "chat.nudge.sent", "You have just sent a Nudge!" },
            { "chat.nudge.received", "{0} just sent you a Nudge!" },
            { "chat.games", "Games" },
            { "chat.smilies", "Smilies" },
            { "chat.nudge", "Nudge" },
            { "chat.sendfile", "Send a file" },
            { "chat.changepicture", "Change your picture" },
            { "chat.sendfile.title", "Send a file to {0}" },
            { "chat.file.toolarge.title", "File is too large" },
            { "chat.file.toolarge.text", "'{0}' is {1}. The largest file you can send is {2}." },
            { "chat.file.failed.title", "Unable to send file" },
            { "chat.file.failed.text", "The file could not be sent. {0}" },
            { "chat.file.unreadable", "That file cannot be read from its current location." },
            { "chat.file.notsaved", "A file from {0} could not be saved. {1}" },

            { "games.tictactoe", "Tic-tac-toe" },
            { "game.invite.sent", "You invited {0} to play {1}." },
            { "game.invite.received", "{0} invited you to play {1}." },
            { "game.waiting", "Waiting for {0} to answer..." },
            { "game.invited", "{0} wants to play {1}." },
            { "game.accept", "Play" },
            { "game.decline", "Decline" },
            { "game.cancel", "Cancel" },
            { "game.yourturn", "Your turn" },
            { "game.theirturn", "{0}'s turn" },
            { "game.youplay", "You are {0}" },
            { "game.won", "You won!" },
            { "game.lost", "{0} won." },
            { "game.draw", "It's a draw." },
            { "game.playagain", "Play again" },
            { "game.leave", "Leave game" },
            { "game.close", "Close" },
            { "game.declined", "{0} declined." },
            { "game.withdrawn", "{0} withdrew the invitation." },
            { "game.left", "{0} left the game." },
            { "game.unavailable", "{0} is not signed in." },
            { "game.signedout", "{0} signed out." },

            { "dialog.ok", "OK" },
            { "dialog.yes", "Yes" },
            { "dialog.no", "No" },
            { "dialog.save", "Save" },
            { "dialog.close", "Close" },
            { "dialog.browse", "Browse" },

            { "addfriend.title", "Add a Contact" },
            { "addfriend.prompt", "Enter a Linkora ID. When you add someone to your contact list they will\nreceive a request asking them to accept or decline.\n\nIf they accept, the contact will appear in your contact list." },

            { "options.title", "Options" },
            { "options.newavatar", "New Avatar" },
            { "options.displayname", "Display name" },
            { "options.language", "Language" },
            { "options.darktheme", "Dark theme" },
            { "options.signedinas", "Signed in as {0}" },
            { "options.username", "Sign in name" },
            { "options.username.hint", "Letters, digits, dots, dashes and underscores" },
            { "options.username.failed.title", "Could not change your sign in name" },
            { "options.username.taken", "Somebody else is already using that name. Try another one." },
            { "options.username.invalid", "A sign in name can be up to 29 characters and may use letters, digits, dots, dashes and underscores." },
            { "options.username.error", "The server could not change your sign in name. Nothing was changed." },
            { "options.username.changed", "You now sign in as {0}." },
            { "options.avatar.disabled.title", "Unable to change avatar." },
            { "options.avatar.disabled.text", "Changing avatar has been disabled." },
            { "options.avatar.failed", "Failed to upload imagine." },
            { "options.avatar.error", "Unable to upload image. {0}" },
            { "options.error", "Error" },

            { "friendrequest.title", "New Friend Request" },
            { "friendrequest.text", "'{0}' ({1}) has added you to his/her contact list.\n\nDo you want to add this person to your own contact list?" },

            { "error.signin.title", "Wrong username / password" },
            { "error.signin.text", "We can't sign you into Linkora" },
            { "error.outdated.title", "Outdated software" },
            { "error.outdated.text", "This version of Linkora is outdated." },
            { "error.server.title", "Unable to reach that server" },
            { "error.server.text", "'{0}' could not be found.\n\nCheck the server address on the sign in page." },
            { "error.connection.title", "Unable to connect to server." },
            { "error.connection.text", "Linkora was not able to contact the server. Please check your internet connection." },
            { "error.unreachable.title", "Unable to reach the server" },
            { "error.unreachable.text", "Linkora could not connect to {0}.\n\nThe server may be offline, or a firewall may be blocking port {1}." },
            { "error.signup.title", "Server address needed" },

            { "notification.signedin", "has just signed in." },
            { "notification.sentfile", "sent you a file." },
            { "notification.sentnudge", "Sent you a nudge!" },
            { "notification.game", "wants to play a game." }
        };
    }
}
