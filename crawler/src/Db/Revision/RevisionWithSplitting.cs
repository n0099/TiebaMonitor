namespace tbm.Crawler.Db.Revision
{
    public abstract class RevisionWithSplitting<TSplitEntities> : IRevision
        where TSplitEntities : IRevision
    {
        public uint TakenAt { get; set; }
        public ushort? NullFieldsBitMask { get; set; }
        public virtual bool IsAllFieldsIsNullExceptSplit() => throw new NotImplementedException();

        public Dictionary<Type, TSplitEntities> SplitEntities { get; } = new();

        protected TValue? GetSplitEntityValue<TSplitEntity, TValue>(Func<TSplitEntity, TValue?> valueSelector)
            where TSplitEntity : class, TSplitEntities =>
            SplitEntities.TryGetValue(typeof(TSplitEntity), out var entity)
                ? valueSelector((TSplitEntity)entity)
                : default;

        protected void SetSplitEntityValue<TSplitEntity, TValue>(TValue? value,
            Action<TSplitEntity, TValue?> valueSetter, Func<TSplitEntity> entityFactory)
            where TSplitEntity : class, TSplitEntities
        {
            if (SplitEntities.TryGetValue(typeof(TSplitEntity), out var entity))
                valueSetter((TSplitEntity)entity, value);
            else
                SplitEntities[typeof(TSplitEntity)] = entityFactory();
        }
    }
}