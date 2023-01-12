// ReSharper disable PropertyCanBeMadeInitOnly.Global
namespace tbm.Crawler.Db.Revision
{
    public class SubReplyRevision : BaseRevision
    {
        public ulong Spid { get; set; }
        public int AgreeCount { get; set; }
        public int DisagreeCount { get; set; }
    }
}
