using System;
using System.Collections.Generic;

using static System.FormattableString;

namespace StepperUpper
{
    internal abstract class DownloadTags
    {
        public static DownloadTags CreateFrom(string tags)
        {
            using (IEnumerator<string> tokens = Program.Tokenize(tags).GetEnumerator())
            {
                tokens.MoveNext();
                string handler = tokens.Current;
                tokens.MoveNext();
                switch (handler)
                {
                    case "steam":
                        return new SteamDownloadTags { AppId = tokens.Current };

                    case "nexus":
                        string game = tokens.Current;
                        tokens.MoveNext();
                        string modId = tokens.Current;
                        tokens.MoveNext();
                        string fileId = tokens.Current;
                        return new NexusDownloadTags { Game = game, ModId = modId, FileId = fileId };

                    case "generic":
                        return new GenericDownloadTags { Url = tokens.Current };

                    default:
                        throw new NotSupportedException("Unrecognized handler: " + handler);
                }
            }
        }

        public abstract override string ToString();

        private sealed class SteamDownloadTags : DownloadTags
        {
            internal string AppId;

            public override string ToString() => Invariant($"steam://store/{this.AppId}");
        }

        private sealed class NexusDownloadTags : DownloadTags
        {
            internal string Game;

            internal string ModId;

            internal string FileId;

            // don't tell Mom
            ////public override string ToString() => Invariant($"nxm://{this.Game}/mods/{this.ModId}/files/{this.FileId}");
            public override string ToString() => Invariant($"http://www.nexusmods.com/{this.Game}/mods/{this.ModId}");
        }

        private sealed class GenericDownloadTags : DownloadTags
        {
            internal string Url;

            public override string ToString() => this.Url;
        }
    }
}
