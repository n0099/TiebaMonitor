import type { RouterConfig } from 'nuxt/schema';
import type { RouteLocation, RouteRecordRaw, RouteRecordRedirect, RouteRecordSingleViewWithChildren, RouterScrollBehavior } from 'vue-router';
import _ from 'lodash';

const withCursorRoute = (component: () => Promise<Component>) =>
    (path: string, name: string): RouteRecordSingleViewWithChildren =>
        ({
            path,
            name,
            component,
            children: [{ // see `App\Http\Controllers\PostsQuery->query()` in be
                // non capture group (?:) and escaping `)` is required for regex in vue route
                path: 'cursor/:cursor((?:(?:[A-Za-z0-9-_]{4}\\)*(?:[A-Za-z0-9-_]{2,3}\\)(?:,|$\\)|,\\){5,6})',
                name: `${name}${routeNameSuffix.cursor}`,
                component
            } as RouteRecordRaw]
        });
const redirectRoute = (before: string, after: string): RouteRecordRedirect[] => [{
    path: `${before}/:pathMatch(.*)*`,
    redirect: to =>
        `${after}/${_.isArray(to.params.pathMatch) ? to.params.pathMatch.join('/') : to.params.pathMatch}`
}, { path: before, redirect: after }];

export default {
    routes: _routes => {
        const postCursorRoute = withCursorRoute(async () => import('@/pages/posts.vue'));
        const postChildren = [
            postCursorRoute('fid/:fid(\\d+)', 'posts/fid'),
            postCursorRoute('tid/:tid(\\d+)', 'posts/tid'),
            postCursorRoute('pid/:pid(\\d+)', 'posts/pid'),
            postCursorRoute('spid/:spid(\\d+)', 'posts/spid'),
            {
                path: ':idType(f|t|p|sp)/:id(\\d+)',
                redirect: (to: RouteLocation) =>
                    _.isString(to.params.idType) && _.isString(to.params.id)
                    && `/posts/${to.params.idType}id/${to.params.id}`
            },
            postCursorRoute(':pathMatch(.*)*', 'posts/param')
        ];

        const userCursorRoute = withCursorRoute(async () => import('@/pages/users.vue'));
        const userChildren = [
            userCursorRoute('id/:uid(\\d+)', 'users/uid'),
            ...redirectRoute('n', '/users/name'),
            userCursorRoute('name/:name', 'users/name'),
            ...redirectRoute('dn', '/users/displayName'),
            userCursorRoute('displayName/:displayName', 'users/displayName')
        ];

        const post = _routes.find(p => p.path === '/posts');
        const user = _routes.find(p => p.path === '/users');

        return [
            ..._routes,
            ...redirectRoute('/p', '/posts'),
            _.merge(post, { children: postChildren }),
            ...redirectRoute('/u', '/users'),
            _.merge(user, { children: userChildren })
        ];
    },
    async scrollBehavior(to, from, savedPosition) {
        if (savedPosition !== null && savedPosition.top !== 0)
            return savedPosition;

        const routeScrollBehavior = useRouteScrollBehaviorStore().get;
        if (routeScrollBehavior !== undefined) {
            const ret: ReturnType<RouterScrollBehavior> | undefined =
                routeScrollBehavior(to, from, savedPosition);
            if (ret !== undefined)
                return ret;
        }

        if (to.hash !== '')
            return { el: to.hash, top: 0 };
        if (from.name !== undefined) { // when user refresh page
            assertRouteNameIsStr(to.name);
            assertRouteNameIsStr(from.name);

            if (!isPathsFirstDirectorySame(to.path, from.path))
                return { top: 0 };
        }

        return false;
    }
} as RouterConfig;
