using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml;

namespace ImportIntellisenseComments
{
    class ParseIntelliSense
    {

        private readonly string _intelliSenseDirectory;
        private Dictionary<string, string> _membersDictionary = new Dictionary<string, string>();

        public string IntelliSenseDirectory
        {
            get
            {
                return _intelliSenseDirectory;
            }
        }

        public Dictionary<string, string> MembersDictionary
        {
            get
            {
                return _membersDictionary;
            }
        }

        public ParseIntelliSense(string intelliSenseDirectory)
        {
            _intelliSenseDirectory = intelliSenseDirectory;
        }

        #region Methods
        public void ParseIntelliSenseFiles()
        {
            foreach (var uidAndReader in from f in EnumerateDevelopCommentFiles()
                                         from uidAndReader in EnumerateDeveloperComments(f)
                                         select uidAndReader)
            {
                try
                {
                    _membersDictionary.Add(uidAndReader.Uid, uidAndReader.Reader.ReadOuterXml());
                }
                catch (ArgumentException)
                {
                    // There should only be a few duplicate that it's fine to just ignore.
                    System.Diagnostics.Debug.WriteLine($"Duplicated entry: {uidAndReader.Uid}");
                }
            }
        }

        private IEnumerable<string> EnumerateDevelopCommentFiles() =>
            Directory.EnumerateFiles(IntelliSenseDirectory, "*.xml", SearchOption.TopDirectoryOnly);

        private IEnumerable<UidAndReader> EnumerateDeveloperComments(string file)
        {
            //Console.WriteLine($"Loading developer comments from file: {file}");
            return from reader in
                       new Func<XmlReader>(() => XmlReader.Create(file))
                       .EmptyIfThrow()
                       .ProtectResource()
                   where reader.ReadToFollowing("members")
                   from apiReader in reader.Elements("member")
                   let commentId = apiReader.GetAttribute("name")
                   where commentId != null && commentId.Length > 2 && commentId[1] == ':'
                   select new UidAndReader { Uid = commentId, Reader = apiReader };
        }
        #endregion

        #region Nested Class

        internal sealed class UidAndReader
        {
            public string Uid { get; set; }
            public XmlReader Reader { get; set; }
        }
        #endregion
    }
}
