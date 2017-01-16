using System;
using System.Globalization;
using System.Xml.Linq;

namespace StepperUpper
{
    internal sealed class CheckedFile
    {
        public string Name;

        public string Option;

        public long LengthInBytes;

        public string CanonicalFileName;

        public Md5Checksum Md5Checksum;

        public Sha512Checksum Sha512Checksum;

        public string DownloadUrl;

        public DownloadTags DownloadTags;

        public CheckedFile() { }

        public CheckedFile(XElement element)
        {
            foreach (var att in element.Attributes())
            {
                switch (att.Name.LocalName)
                {
                    case "Name":
                        this.Name = att.Value;
                        break;

                    case "Option":
                        this.Option = att.Value;
                        break;

                    case "LengthInBytes":
                        this.LengthInBytes = Int64.Parse(att.Value, NumberStyles.None, CultureInfo.InvariantCulture);
                        break;

                    case "CanonicalFileName":
                        this.CanonicalFileName = att.Value;
                        break;

                    case "MD5Checksum":
                        this.Md5Checksum = new Md5Checksum(att.Value);
                        break;

                    case "SHA512Checksum":
                        this.Sha512Checksum = new Sha512Checksum(att.Value);
                        break;

                    case "DownloadUrl":
                        this.DownloadUrl = att.Value;
                        break;

                    case "DownloadTags":
                        this.DownloadTags = DownloadTags.CreateFrom(att.Value);
                        break;
                }
            }
        }
    }
}
