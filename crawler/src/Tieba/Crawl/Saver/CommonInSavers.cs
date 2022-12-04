namespace tbm.Crawler.Tieba.Crawl.Saver
{
    public abstract class CommonInSavers<TSaver> : StaticCommonInSavers where TSaver : CommonInSavers<TSaver>
    {
        private readonly ILogger<CommonInSavers<TSaver>> _logger;

        protected CommonInSavers(ILogger<CommonInSavers<TSaver>> logger) => _logger = logger;

        protected virtual Dictionary<string, ushort> RevisionNullFieldsBitMasks => null!;

        protected void SavePostsOrUsers<TPostIdOrUid, TPostOrUser, TRevision>(
            FieldChangeIgnoranceCallbackRecord tiebaUserFieldChangeIgnorance,
            IDictionary<TPostIdOrUid, TPostOrUser> postsOrUsers,
            TbmDbContext db,
            Func<TPostOrUser, TRevision> revisionFactory,
            Func<TPostOrUser, bool> isExistPredicate,
            Func<TPostOrUser, TPostOrUser> existedSelector)
            where TPostOrUser : class where TRevision : BaseRevision
        {
            var existedOrNew = postsOrUsers.Values.ToLookup(isExistPredicate);
            db.Set<TPostOrUser>().AddRange(existedOrNew[false]); // newly added
            db.AddRange(existedOrNew[true].Select(newPostOrUser =>
            {
                var postOrUserInTracking = existedSelector(newPostOrUser);
                var entry = db.Entry(postOrUserInTracking);
                entry.CurrentValues.SetValues(newPostOrUser); // this will mutate postOrUserInTracking which is referenced by entry

                // rollback writes on the fields of IEntityWithTimestampFields with the default value 0, this will also affects postOrUserInTracking
                entry.Properties.Where(p => p.Metadata.Name is nameof(IEntityWithTimestampFields.CreatedAt)
                    or nameof(IEntityWithTimestampFields.UpdatedAt)).ForEach(p => p.IsModified = false);

                var revision = default(TRevision);
                int? revisionNullFieldsBitMask = null;
                var whichPostType = typeof(TPostOrUser);
                var entryIsUser = whichPostType == typeof(TiebaUser);
                foreach (var p in entry.Properties)
                {
                    var pName = p.Metadata.Name;
                    if (!p.IsModified || pName is nameof(IPost.LastSeen)
                            or nameof(IEntityWithTimestampFields.CreatedAt)
                            or nameof(IEntityWithTimestampFields.UpdatedAt)) continue;

                    if (FieldChangeIgnorance.Update(whichPostType, pName, p.OriginalValue, p.CurrentValue)
                        || (entryIsUser && tiebaUserFieldChangeIgnorance.Update(whichPostType, pName, p.OriginalValue, p.CurrentValue)))
                    {
                        p.IsModified = false;
                        continue; // skip following revision check
                    }
                    if (FieldChangeIgnorance.Revision(whichPostType, pName, p.OriginalValue, p.CurrentValue)
                        || (entryIsUser && tiebaUserFieldChangeIgnorance.Revision(whichPostType, pName, p.OriginalValue, p.CurrentValue))) continue;

                    // ThreadCrawlFacade.ParseLatestRepliers() will save users with empty string as portrait
                    // they will soon be updated by (sub) reply crawler after it find out the latest reply
                    // so we should ignore its revision update for all fields
                    // ignore entire record is not possible via FieldChangeIgnorance.Revision() since it can only determine one field at the time
                    if (entryIsUser && pName == nameof(TiebaUser.Portrait) && p.OriginalValue is "")
                    {
                        // invokes OriginalValues.ToObject() to get a new instance since postOrUserInTracking is reference to the changed one
                        var user = (TiebaUser)entry.OriginalValues.ToObject();
                        // create another user instance with only fields of latest replier filled
                        var latestReplier = ThreadCrawlFacade.LatestReplierFactory(user.Uid, user.Name, user.DisplayName);
                        // if they are same by fields values, the original one is a latest replier that previously generated by ParseLatestRepliers()
                        if (user.Equals(latestReplier)) return null;
                    }

                    var revisionProp = RevisionPropertiesCache[typeof(TRevision)].FirstOrDefault(p2 => p2.Name == pName);
                    if (revisionProp == null)
                    {
                        object? ToHexWhenByteArray(object? value) => value is byte[] bytes ? "0x" + Convert.ToHexString(bytes).ToLowerInvariant() : value;
                        _logger.LogWarning("Updating field {} is not existed in revision table, " +
                                          "newValue={}, oldValue={}, newObject={}, oldObject={}",
                            pName, ToHexWhenByteArray(p.CurrentValue), ToHexWhenByteArray(p.OriginalValue),
                            Helper.UnescapedJsonSerialize(newPostOrUser), Helper.UnescapedJsonSerialize(entry.OriginalValues.ToObject()));
                    }
                    else
                    {
                        revision ??= revisionFactory(postOrUserInTracking);
                        revisionProp.SetValue(revision, p.OriginalValue);

                        if (p.OriginalValue != null) continue;
                        revisionNullFieldsBitMask ??= 0;
                        // mask the corresponding field bit with 1
                        revisionNullFieldsBitMask |= RevisionNullFieldsBitMasks[pName];
                    }
                }
                if (revision != null) revision.NullFieldsBitMask = (ushort?)revisionNullFieldsBitMask;
                return revision;
            }).OfType<TRevision>());
        }
    }
}
