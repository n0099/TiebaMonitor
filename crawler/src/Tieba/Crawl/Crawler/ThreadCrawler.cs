namespace tbm.Crawler
{
    public sealed class ThreadCrawler : BaseCrawler<ThreadResponse, Thread>
    {
        private readonly string _forumName;

        public delegate ThreadCrawler New(string forumName);

        public ThreadCrawler(ClientRequester requester, string forumName) : base(requester) => _forumName = forumName;

        public override Exception FillExceptionData(Exception e)
        {
            e.Data["forumName"] = _forumName;
            return e;
        }

        protected override IEnumerable<(Task<ThreadResponse>, CrawlRequestFlag, Page)> RequestsFactory(Page page)
        {
            const string url = "http://c.tieba.baidu.com/c/f/frs/page?cmd=301001";
            var requestBody602 = new ThreadRequest.Types.Data
            {
                Kw = _forumName,
                Pn = (int)page,
                Rn = 30
            };
            var requestBody = new ThreadRequest.Types.Data
            {
                Kw = _forumName,
                Pn = (int)page,
                Rn = 90,
                RnNeed = 30,
                QType = 2,
                SortType = 5
            };
            return new[]
            {
                (Requester.RequestProtoBuf(url, responseFactory: () => new ThreadResponse(),
                    param: new ThreadRequest {Data = requestBody}, clientVersion: "12.23.1.0"), CrawlRequestFlag.None, page),
                (Requester.RequestProtoBuf(url, responseFactory: () => new ThreadResponse(),
                    param: new ThreadRequest {Data = requestBody602}, clientVersion: "6.0.2"), CrawlRequestFlag.Thread602ClientVersion, page)
            };
        }

        public override IList<Thread> GetValidPosts(ThreadResponse response)
        {
            ValidateOtherErrorCode(response);
            return EnsureNonEmptyPostList(response, 7,
                "Forum threads list is empty, forum might doesn't existed");
        }
    }
}
