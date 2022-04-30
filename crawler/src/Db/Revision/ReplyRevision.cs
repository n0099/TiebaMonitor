namespace tbm.Crawler
{
    public class ReplyRevision : PostRevision
    {
        public ulong Pid { get; set; }
        public string? AuthorManagerType { get; set; }
        public ushort? AuthorExpGrade { get; set; }
        public int? SubReplyNum { get; set; }
        public ushort? IsFold { get; set; }
        public int? AgreeNum { get; set; }
        public int? DisagreeNum { get; set; }
    }
}