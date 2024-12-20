export interface ApiError { errorCode: number, errorInfo: Record<string, unknown[]> | string }
export interface Api<TResponse, TQueryParam = never> {
    response: TResponse,
    queryParam: TQueryParam
}

export type ApiForums = Api<Array<{
    id: UInt,
    fid: Fid,
    name: string,
    isCrawling: BoolInt
}>>;

export type Cursor = string;
export interface CursorPagination {
    pages: {
        currentCursor: Cursor,
        nextCursor: Cursor | null
    }
}

export type ApiUsers = Api<
    CursorPagination & { users: User[] },
    SelectUserParams & { gender?: UserGenderQueryParam }
>;

export type JsonString = string;
export type ApiPosts = Api<CursorPagination & {
    pages: {
        matchQueryPostCount: Record<PostType, UInt>,
        notMatchQueryParentPostCount: Record<Exclude<PostType, 'subRely'>, UInt>
    },
    type: 'index' | 'search',
    forum: Pick<ApiForums['response'][number], 'fid' | 'name'>,
    threads: Array<Thread & {
        replies: Array<Reply & {
            subReplies: SubReply[]
        }>
    }>,
    users: User[],
    latestRepliers: LatestReplier[]
}, { query: JsonString }>;
export type ApiForumThreadsID = Api<CursorPagination & {
    tid: Tid[]
}, { cursor: Tid }>;
