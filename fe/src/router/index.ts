import { compareRouteIsNewQuery } from '@/shared';
import Index from '@/views/Index.vue';
import type { Component } from 'vue';
import { createRouter, createWebHistory } from 'vue-router';
import NProgress from 'nprogress';

const lazyLoadRouteView = async (component: Promise<Component>) => {
    NProgress.start();
    const loadingBlocksDom = document.getElementById('loadingBlocksRouteChange');
    const [containerDom] = document.getElementsByClassName('container');
    loadingBlocksDom?.classList.remove('d-none');
    containerDom.classList.add('invisible');
    return component.finally(() => {
        NProgress.done();
        loadingBlocksDom?.classList.add('d-none');
        containerDom.classList.remove('invisible');
    });
};

const userRoute = { component: async () => lazyLoadRouteView(import('@/views/User.vue')), props: true };
const postRoute = { components: { escapeContainer: async () => lazyLoadRouteView(import('@/views/Post.vue')) }, props: true };
export default createRouter({
    history: createWebHistory(process.env.VUE_APP_PUBLIC_PATH),
    routes: [
        { path: '/', name: 'index', component: Index },
        {
            path: '/p',
            name: 'post',
            ...postRoute,
            children: [
                { path: 'page/:page', name: 'post+p', ...postRoute },
                { path: 'f/:fid', name: 'fid', ...postRoute, children: [{ path: 'page/:page', name: 'fid+p', ...postRoute }] },
                { path: 't/:tid', name: 'tid', ...postRoute, children: [{ path: 'page/:page', name: 'tid+p', ...postRoute }] },
                { path: 'p/:pid', name: 'pid', ...postRoute, children: [{ path: 'page/:page', name: 'pid+p', ...postRoute }] },
                { path: 'sp/:spid', name: 'spid', ...postRoute, children: [{ path: 'page/:page', name: 'spid+p', ...postRoute }] }
            ]
        },
        {
            path: '/u',
            name: 'user',
            ...userRoute,
            children: [
                { path: 'page/:page', name: 'user+p', ...userRoute },
                { path: 'id/:uid', name: 'uid', ...userRoute, children: [{ path: 'page/:page', name: 'uid+p', ...userRoute }] },
                { path: 'n/:name', name: 'name', ...userRoute, children: [{ path: 'page/:page', name: 'name+p', ...userRoute }] },
                { path: 'dn/:displayName', name: 'displayName', ...userRoute, children: [{ path: 'page/:page', name: 'displayName+p', ...userRoute }] }
            ]
        },
        { path: '/status', name: 'status', component: async () => lazyLoadRouteView(import('@/views/Status.vue')) },
        { path: '/stats', name: 'stats', component: async () => lazyLoadRouteView(import('@/views/Stats.vue')) },
        { path: '/bilibiliVote', name: 'bilibiliVote', component: async () => lazyLoadRouteView(import('@/views/BilibiliVote.vue')) }
    ],
    linkActiveClass: 'active',
    scrollBehavior(to, from, savedPosition) {
        if (('page' in from.params || 'page' in to.params)
            && !compareRouteIsNewQuery(to, from)) return { el: `#page${to.params.page ?? 1}`, top: 0 };
        // the undocumented 'href' property will not in from when user refresh page
        if (!('href' in from || savedPosition === null)) return savedPosition;
        return { top: 0 };
    }
});
