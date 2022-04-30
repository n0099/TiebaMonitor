namespace tbm.Crawler
{
    public abstract class CommonInSavers<T> where T : CommonInSavers<T>
    {
        protected void SavePostsOrUsers<TPostIdOrUid, TPostOrUser, TRevision>(
            ILogger<CommonInSavers<T>> logger,
            TbmDbContext db,
            bool shouldIgnoreUpdatesOnGender,
            IDictionary<TPostIdOrUid, TPostOrUser> postsOrUsers,
            Func<TPostOrUser, TRevision> revisionFactory,
            Func<TPostOrUser, bool> isExistPredicate,
            Func<TPostOrUser, TPostOrUser> existedSelector)
        {
            var existedOrNew = postsOrUsers.Values.ToLookup(isExistPredicate);
            db.AddRange((IEnumerable<object>)GetRevisionsForObjectsThenMerge(existedOrNew[true], existedSelector, revisionFactory, logger, shouldIgnoreUpdatesOnGender));
            var newPostsOrUsers = ((IEnumerable<object>)existedOrNew[false]).ToList();
            if (newPostsOrUsers.Any()) db.AddRange(newPostsOrUsers);
        }

        private static IEnumerable<TRevision> GetRevisionsForObjectsThenMerge<TObject, TRevision>(
            IEnumerable<TObject> newObjects,
            Func<TObject, TObject> oldObjectSelector,
            Func<TObject, TRevision> revisionFactory,
            ILogger<CommonInSavers<T>> logger,
            bool shouldIgnoreUpdatesOnGender)
        {
            var objectProps = typeof(TObject).GetProperties()
                .Where(p => p.Name is not (nameof(IEntityWithTimestampFields.CreatedAt) or nameof(IEntityWithTimestampFields.UpdatedAt))).ToList();
            var revisionProps = typeof(TRevision).GetProperties();

            return newObjects.Select(newObj =>
            {
                var revision = default(TRevision);
                var oldObj = oldObjectSelector(newObj);
                foreach (var p in objectProps)
                {
                    var newValue = p.GetValue(newObj);
                    var oldValue = p.GetValue(oldObj);
                    var isBlobEqual = false;
                    if (oldValue is byte[] o && newValue is byte[] n)
                    { // https://stackoverflow.com/questions/43289/comparing-two-byte-arrays-in-net/48599119#48599119
                        static bool ByteArrayCompare(ReadOnlySpan<byte> a1, ReadOnlySpan<byte> a2) => a1.SequenceEqual(a2);
                        isBlobEqual = ByteArrayCompare(o, n);
                    }

                    if (isBlobEqual || Equals(oldValue, newValue)
                                    // // the value of user gender returned by thread crawler is always 0, so we shouldn't update existing value that is set before
                                    || (shouldIgnoreUpdatesOnGender && p.Name == nameof(TiebaUser.Gender))) continue;
                    // tell ef core to update field p of record oldObj with newValue
                    // ef core is able to track changes made on oldObj via reflection
                    p.SetValue(oldObj, newValue);

                    var revisionProp = revisionProps.FirstOrDefault(p2 => p2.Name == p.Name);
                    if (revisionProp == null)
                    {
                        if (p.Name != nameof(ThreadPost.Title)) // thread title might be set by ReplyCrawlFacade.PostParseCallback()
                            logger.LogWarning("Updating field {} is not existed in revision table, " +
                                              "newValue={}, oldValue={}, newObject={}, oldObject={}",
                                p.Name, newValue, oldValue, JsonSerializer.Serialize(newObj), JsonSerializer.Serialize(oldObj));
                    }
                    else
                    {
                        revision ??= revisionFactory(oldObj);
                        revisionProp.SetValue(revision, oldValue);
                    }
                }

                return revision;
            }).OfType<TRevision>();
        }
    }
}