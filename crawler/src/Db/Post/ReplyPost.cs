// ReSharper disable UnusedAutoPropertyAccessor.Global

using TbClient.Post.Common;

namespace tbm.Crawler.Db.Post;

public class ReplyPost : IPost, IPostWithAuthorExpGrade
{
    public object Clone() => MemberwiseClone();
    public ulong Tid { get; set; }
    [Key] public ulong Pid { get; set; }
    public uint Floor { get; set; }
    [NotMapped] public byte[]? Content { get; set; }
    [NotMapped] public RepeatedField<Content>? OriginalContents { get; set; }
    public long AuthorUid { get; set; }
    [NotMapped] public ushort AuthorExpGrade { get; set; }
    public uint? SubReplyCount { get; set; }
    public uint PostedAt { get; set; }
    public ushort? IsFold { get; set; }
    public int? AgreeCount { get; set; }
    public int? DisagreeCount { get; set; }
    public byte[]? Geolocation { get; set; }
    public uint? SignatureId { get; set; }
    [NotMapped] public byte[]? Signature { get; set; }
    public uint CreatedAt { get; set; }
    public uint? UpdatedAt { get; set; }
    public uint? LastSeenAt { get; set; }
}
