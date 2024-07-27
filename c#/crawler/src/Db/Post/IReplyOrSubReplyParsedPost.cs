namespace tbm.Crawler.Db.Post;

public interface IReplyOrSubReplyParsedPost : IPost.IParsed
{
    public byte AuthorExpGrade { get; set; }
    public byte[]? Content { get; set; }
    [JsonConverter(typeof(ProtoBufRepeatedFieldJsonConverter<Content>))]
    public RepeatedField<Content> ContentsProtoBuf { get; set; }
}