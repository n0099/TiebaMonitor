import Index from '@/views/Index.vue';
import type { Component } from 'vue';
import type { RouteLocationNormalized, RouteLocationNormalizedLoaded, RouteRecordRaw } from 'vue-router';
import { createRouter, createWebHistory } from 'vue-router';
import NProgress from 'nprogress';
import _ from 'lodash';

export const routeNameStrAssert: (name: RouteLocationNormalizedLoaded['name']) => asserts name is string = name => {
    if (!_.isString(name)) throw Error('https://github.com/vuejs/vue-router-next/issues/1185');
}; // https://github.com/microsoft/TypeScript/issues/34523#issuecomment-700491122
export const compareRouteIsNewQuery = (to: RouteLocationNormalized, from: RouteLocationNormalized) =>
    !(_.isEqual(to.query, from.query) && _.isEqual(_.omit(to.params, 'page'), _.omit(from.params, 'page')));

const lazyLoadRouteView = async (component: Promise<Component>) => {
    NProgress.start();
    const loadingBlocksDom = document.getElementById('loadingBlocksRouteChange');
    const containersDom = document.getElementsByClassName('container');
    loadingBlocksDom?.classList.remove('d-none');
    containersDom.forEach(i => { i.classList.add('d-none') });
    return component.finally(() => {
        NProgress.done();
        loadingBlocksDom?.classList.add('d-none');
        containersDom.forEach(i => { i.classList.remove('d-none') });
    });
};
const withPageRoute = <T extends { components: Record<string, () => Promise<Component>> }
| { component: () => Promise<Component> } & { props: boolean }>
(routeName: string, restRoute: T): T & { children: RouteRecordRaw[] } =>
    ({ ...restRoute, children: [{ ...restRoute, path: 'page/:page', name: `${routeName}+p` }] });

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
                { path: 'f/:fid', name: 'post/fid', ...withPageRoute('post/fid', postRoute) },
                { path: 't/:tid', name: 'post/tid', ...withPageRoute('post/tid', postRoute) },
                { path: 'p/:pid', name: 'post/pid', ...withPageRoute('post/pid', postRoute) },
                { path: 'sp/:spid', name: 'post/spid', ...withPageRoute('post/spid', postRoute) },
                { path: ':pathMatch(.*)*', name: 'post/param', ...postRoute }
            ]
        },
        {
            path: '/u',
            name: 'user',
            ...userRoute,
            children: [
                { path: 'page/:page', name: 'user+p', ...userRoute },
                { path: 'id/:uid', name: 'user/uid', ...withPageRoute('user/uid', userRoute) },
                { path: 'n/:name', name: 'user/name', ...withPageRoute('user/name', userRoute) },
                { path: 'dn/:displayName', name: 'user/displayName', ...withPageRoute('user/displayName', userRoute) }
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
        if (to.hash) return { el: to.hash, top: 0 };
        // the undocumented 'href' property will not in from when user refresh page
        if (!('href' in from || savedPosition === null)) return savedPosition;
        routeNameStrAssert(to.name);
        routeNameStrAssert(from.name);
        if (to.name.split('/')[0] !== from.name.split('/')[0]) return { top: 0 };
        return false;
    }
});
