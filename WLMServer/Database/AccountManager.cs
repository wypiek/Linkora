using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Data;
using MySql.Data.MySqlClient;

using WLMData.Data.Packets;

namespace WLMServer.Database
{
    class AccountManager : DBConnection
    {
        public void UpdateAccount(string id, string name, string comment, string avatar)
        {
            lock (databaseLocker)
            {
                MySqlCommand cmd = new MySqlCommand();
                cmd.Parameters.AddWithValue("@0", id);
                cmd.Parameters.AddWithValue("@1", name);
                cmd.Parameters.AddWithValue("@2", comment);
                cmd.Parameters.AddWithValue("@3", avatar);

                Write("UPDATE account SET name=@1, comment=@2, avatar=@3 WHERE id=@0", cmd);
            }
        }

        /// <summary>
        /// Characters allowed in a sign in name. Kept narrow deliberately: contact lists are stored
        /// as "[id,blocked,accepted]" text, so a name containing a bracket or comma would corrupt
        /// every list that references it.
        /// </summary>
        public static bool IsValidUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return false;
            }

            username = username.Trim();

            if (username.Length == 0 || username.Length >= 30)
            {
                return false;
            }

            foreach (char character in username)
            {
                bool allowed = char.IsLetterOrDigit(character) ||
                    character == '.' || character == '_' || character == '-';

                if (!allowed)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Renames an account and every reference to it: the contact lists of everyone who has it,
        /// and any outstanding friend requests. All of it in one transaction, so a failure part way
        /// through cannot leave contact lists pointing at a name that no longer exists.
        /// </summary>
        public bool RenameAccount(string oldId, string newId)
        {
            lock (databaseLocker)
            {
                CheckDatabaseAccess();

                MySqlTransaction transaction = dbConnection.BeginTransaction();

                try
                {
                    Execute(transaction, "UPDATE account SET id=@new WHERE id=@old", oldId, newId);

                    // Replace the "[id," token rather than the bare name, so an account whose name is a
                    // prefix of another ("bob" against "bobby") is left alone.
                    Execute(transaction,
                        "UPDATE account SET contacts = REPLACE(contacts, CONCAT('[', @old, ','), CONCAT('[', @new, ','))" +
                        " WHERE contacts LIKE CONCAT('%[', @old, ',%')", oldId, newId);

                    Execute(transaction, "UPDATE friend_requests SET requesterID=@new WHERE requesterID=@old", oldId, newId);
                    Execute(transaction, "UPDATE friend_requests SET targetID=@new WHERE targetID=@old", oldId, newId);

                    transaction.Commit();

                    return true;
                }
                catch (Exception exception)
                {
                    Program.WriteToConsole("Renaming '" + oldId + "' failed, nothing was changed: " + exception.Message);

                    try
                    {
                        transaction.Rollback();
                    }
                    catch
                    {
                        // Nothing further to do; the transaction never committed.
                    }

                    return false;
                }
                finally
                {
                    transaction.Dispose();
                }
            }
        }

        private void Execute(MySqlTransaction transaction, string query, string oldId, string newId)
        {
            MySqlCommand command = new MySqlCommand();

            command.Connection = dbConnection;
            command.Transaction = transaction;
            command.CommandType = CommandType.Text;
            command.CommandText = query;

            command.Parameters.AddWithValue("@old", oldId);
            command.Parameters.AddWithValue("@new", newId);

            command.ExecuteNonQuery();
        }

        public void InsertNewAccount(string username, string password)
        {
            lock (databaseLocker)
            {
                password = PasswordEncrypter.GetEncryptedPassword(password);

                MySqlCommand cmd = new MySqlCommand();
                cmd.Parameters.AddWithValue("@0", username);
                cmd.Parameters.AddWithValue("@1", password);

                Write("INSERT INTO account VALUES(@0, 'New User', @1, '', '', '')", cmd);
            }
        }

        public bool IsUserInDatabase(string userID)
        {
            lock (databaseLocker)
            {
                bool returnValue = false;

                MySqlCommand cmd = new MySqlCommand();
                cmd.Parameters.AddWithValue("@0", userID);

                MySqlDataReader reader = Read("SELECT id FROM account WHERE id=@0", cmd);
                while (reader.Read())
                {
                    if (reader.GetString("id") == userID)
                    {
                        returnValue = true;
                        break;
                    }
                }

                reader.Close();

                return returnValue;
            }
        }

        public bool AuthenticateAccount(string username, string password, out UserInfo userInfo)
        {
            lock (databaseLocker)
            {
                bool returnValue = false;
                userInfo = null;

                MySqlCommand cmd = new MySqlCommand();
                cmd.Parameters.AddWithValue("@0", username);
                cmd.Parameters.AddWithValue("@1", password);

                string encryptedPassword = PasswordEncrypter.GetEncryptedPassword(password);

                MySqlDataReader reader = Read("SELECT * FROM account WHERE id=@0", cmd);
                while (reader.Read())
                {
                    if (reader.GetString("password").Equals(encryptedPassword))
                    {
                        // Only return userInfo if the password is correct.
                        userInfo = new UserInfo(reader.GetString("id"), reader.GetString("name"), reader.GetString("comment"), 1, reader.GetString("avatar"), false);

                        if (!Config.Properties.AVATAR_ENABLE)
                        {
                            userInfo.avatar = "";
                        }
                        else
                        {
                            Uri baseUri = new Uri(Config.Properties.AVATAR_IMAGE_URL);
                            Uri address = new Uri(baseUri, userInfo.avatar);

                            userInfo.avatar = address.ToString();
                        }

                        returnValue = true;
                    }

                    break;
                }

                reader.Close();
                return returnValue;
            }
        }

        public UserInfo GetUser(string id)
        {
            lock (databaseLocker)
            {
                MySqlCommand cmd = new MySqlCommand();
                cmd.Parameters.AddWithValue("@0", id);

                MySqlDataReader reader = Read("SELECT * FROM account WHERE id=@0", cmd);

                UserInfo userInfo = null;
                while (reader.Read())
                {
                    userInfo = new UserInfo(reader.GetString("id"),
                        reader.GetString("name"), reader.GetString("comment"), 0, reader.GetString("avatar"), false);
                    break;
                }

                reader.Close();

                if (userInfo == null)
                {
                    // No such account. Callers already handle a null user, and dereferencing here
                    // would take the whole server down.
                    return null;
                }

                if (!Config.Properties.AVATAR_ENABLE)
                {
                    userInfo.avatar = "";
                }
                else
                {
                    Uri baseUri = new Uri(Config.Properties.AVATAR_IMAGE_URL);
                    Uri address = new Uri(baseUri, userInfo.avatar);

                    userInfo.avatar = address.ToString();
                }

                return userInfo;
            }
        }

        public List<string> GetFriendRequests(string requesterID)
        {
            lock (databaseLocker)
            {
                List<string> requests = new List<string>();

                MySqlCommand cmd = new MySqlCommand();
                cmd.Parameters.AddWithValue("@0", requesterID);

                MySqlDataReader reader = Read("SELECT requesterID FROM friend_requests WHERE targetID=@0", cmd);

                while (reader.Read())
                {
                    requests.Add(reader.GetString("requesterID"));

                    break;
                }

                reader.Close();

                return requests;
            }
        }

        public string GetContacts(string id)
        {
            lock (databaseLocker)
            {
                MySqlCommand cmd = new MySqlCommand();
                cmd.Parameters.AddWithValue("@0", id);

                string contacts = "";
                MySqlDataReader reader = Read("SELECT contacts FROM account WHERE id=@0", cmd);
                while (reader.Read())
                {
                    contacts = reader.GetString("contacts");

                    break;
                }

                reader.Close();

                return contacts;
            }
        }

        public void SaveContactData(string id, string contactData)
        {
            lock (databaseLocker)
            {
                MySqlCommand cmd = new MySqlCommand();
                cmd.Parameters.AddWithValue("@0", id);
                cmd.Parameters.AddWithValue("@1", contactData);

                Write("UPDATE account SET contacts=@1 WHERE id=@0", cmd);
            }
        }

        public void AddNewFriendRequest(string requesterID, string targetID)
        {
            lock (databaseLocker)
            {
                MySqlCommand cmd = new MySqlCommand();
                cmd.Parameters.AddWithValue("@0", requesterID);
                cmd.Parameters.AddWithValue("@1", targetID);

                Write("INSERT INTO friend_requests VALUES(@0, @1)", cmd);
            }
        }

        public void RemoveFriendRequest(string requesterID, string targetID)
        {
            lock (databaseLocker)
            {
                MySqlCommand cmd = new MySqlCommand();
                cmd.Parameters.AddWithValue("@0", requesterID);
                cmd.Parameters.AddWithValue("@1", targetID);

                Write("DELETE FROM friend_requests WHERE requesterID=@0 AND targetID=@1", cmd);
            }
        }
    }
}
