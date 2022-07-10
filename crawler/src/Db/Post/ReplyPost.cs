namespace tbm.Crawler
{
    public class ReplyPost : IPost
    {
        public object Clone() => MemberwiseClone();
        public ulong Tid { get; set; }
        [Key] public ulong Pid { get; set; }
        public uint Floor { get; set; }
        [NotMapped] public byte[]? Content { get; set; }
        public long AuthorUid { get; set; }
        public string? AuthorManagerType { get; set; }
        public ushort? AuthorExpGrade { get; set; }
        public int SubReplyNum { get; set; }
        public uint PostTime { get; set; }
        public ushort IsFold { get; set; }
        public int AgreeNum { get; set; }
        public int DisagreeNum { get; set; }
        public byte[]? Location { get; set; }
        public uint? SignatureId { get; set; }
        [NotMapped] public byte[]? Signature { get; set; }
        public uint CreatedAt { get; set; }
        public uint UpdatedAt { get; set; }
    }
}
