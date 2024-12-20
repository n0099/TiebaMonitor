import type { QueryFormDeps } from '@/utils/post/queryForm';
import type { InfiniteData } from '@tanstack/vue-query';

export const usePostsSEO = (
    data: Ref<InfiniteData<ApiPosts['response']> | undefined>,
    currentQueryType: QueryFormDeps['currentQueryType']
) => {
    const route = useRoute();
    const firstPostPage = computed(() => data.value?.pages[0]);
    const firstPostPageForum = computed(() => firstPostPage.value?.forum);
    const firstThread = computed(() => firstPostPage.value?.threads[0]);
    useHead({
        title: computed(() => {
            if (firstPostPage.value === undefined)
                return '帖子查询';
            switch (currentQueryType.value) {
                case 'fid':
                case 'search':
                    return `${firstPostPageForum.value?.name}吧 - 帖子查询`;
                case 'postID':
                    return `${firstThread.value?.title} - ${firstPostPageForum.value?.name}吧 - 帖子查询`;
                case 'empty':
            }

            return '帖子查询';
        })
    });
    defineOgImageComponent('Post', { routePath: route.path, firstPostPage, firstPostPageForum, firstThread, currentQueryType });
    usePostsSchemaOrg(data);
};
