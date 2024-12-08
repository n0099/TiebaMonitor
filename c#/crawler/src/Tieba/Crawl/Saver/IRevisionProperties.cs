namespace tbm.Crawler.Tieba.Crawl.Saver;

public interface IRevisionProperties
{
    protected static IReadOnlyDictionary<Type, IReadOnlyDictionary<string, PropertyInfo>> Cache { get; } = GetPropsKeyByType(
        [typeof(ThreadRevision), typeof(ReplyRevision), typeof(SubReplyRevision), typeof(UserRevision)]);

    private static IReadOnlyDictionary<Type, IReadOnlyDictionary<string, PropertyInfo>> GetPropsKeyByType(IEnumerable<Type> types) =>
        types.ToDictionary(type => type,
            IReadOnlyDictionary<string, PropertyInfo> (type) =>
            type.GetProperties().ToDictionary(prop => prop.Name));
}
