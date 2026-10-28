using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Avalonia.Platform;

using WLMClient.Compat;

namespace WLMClient.Resource.Sounds
{
    class Player
    {
        private static readonly Dictionary<string, string> extractedFiles = new Dictionary<string, string>();
        private static readonly object locker = new object();

        /// <summary>
        /// Plays one of the embedded notification sounds. The WAV is unpacked to a temporary file
        /// the first time it is used, then handed to the platform's audio player.
        /// </summary>
        public static void PlaySound(string url)
        {
            try
            {
                string path = GetExtractedPath(url);

                if (path != null)
                {
                    Platform.PlayWavFile(path);
                }
            }
            catch
            {
                // Sound is decorative; never let it interrupt the conversation.
            }
        }

        private static string GetExtractedPath(string url)
        {
            lock (locker)
            {
                string existing;
                if (extractedFiles.TryGetValue(url, out existing))
                {
                    return existing;
                }

                Uri uri = new Uri(url);

                string directory = Path.Combine(Path.GetTempPath(), "WLMClient-sounds");
                Directory.CreateDirectory(directory);

                string path = Path.Combine(directory, Path.GetFileName(uri.AbsolutePath));

                using (Stream source = AssetLoader.Open(uri))
                using (FileStream target = File.Create(path))
                {
                    source.CopyTo(target);
                }

                extractedFiles[url] = path;

                return path;
            }
        }
    }
}
