namespace tbm.Crawler
{
    public class ThreadRevision : IPostRevision
    {
        public object Clone() => MemberwiseClone();
        public uint Time { get; set; }
        public ulong Tid { get; set; }
        public string? StickyType { get; set; }
        public string? TopicType { get; set; }
        public bool? IsGood { get; set; }
        public string? AuthorManagerType { get; set; }
        public uint? LatestReplyTime { get; set; }
        public long? LatestReplierUid { get; set; }
        public uint? ReplyNum { get; set; }
        public uint? ViewNum { get; set; }
        public uint? ShareNum { get; set; }
        public int? AgreeNum { get; set; }
        public int? DisagreeNum { get; set; }
    }
}