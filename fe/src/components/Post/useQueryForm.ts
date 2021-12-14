import type { ObjUnknown } from '@/shared';
import { boolStrToBool } from '@/shared';
import { onBeforeMount, reactive, watch } from 'vue';
import type { RouteLocationNormalizedLoaded } from 'vue-router';
import { onBeforeRouteUpdate, useRoute, useRouter } from 'vue-router';
import _ from 'lodash';

export interface Param { name: string, value: unknown, subParam: ObjUnknown & { not?: boolean } }
export interface ParamPartialValue { value?: unknown, subParam?: ObjUnknown }
export type ParamPartial = ParamPartialValue & { name: string };
export type ParamPreprocessorOrWatcher = (p: Param) => void;
export default <
    UniqueParams extends Record<string, Param> = Record<string, Param>,
    Params extends Record<string, Param> = Record<string, Param>
>(
    deps: {
        paramsDefaultValue: Readonly<Partial<Record<string, ParamPartialValue>>>,
        paramsPreprocessor: Readonly<Partial<Record<string, ParamPreprocessorOrWatcher>>>,
        paramsWatcher: Readonly<Partial<Record<string, ParamPreprocessorOrWatcher>>>,
        parseRoute?: (route: RouteLocationNormalizedLoaded) => void
    }
) => {
    type TUniqueParam = UniqueParams[keyof UniqueParams];
    type TParam = Params[keyof Params];
    const route = useRoute();
    const router = useRouter();
    interface State {
        uniqueParams: UniqueParams,
        params: TParam[],
        invalidParamsIndex: number[]
    }
    const state = reactive<State>({
        uniqueParams: {} as UniqueParams,
        params: [], // [{ name: '', value: '', subParam: { name: value } },...]
        invalidParamsIndex: []
    }) as State; // https://github.com/vuejs/vue-next/issues/1324, https://github.com/vuejs/vue-next/issues/2136

    const paramRowLastDomClass = (paramIndex: number, params: typeof state.params) =>
        (params.length === 1
            ? {} // if there only have one row, class remains unchanged
            : {
                'param-control-first-row': paramIndex === 0,
                'param-control-middle-row': !(paramIndex === 0 || paramIndex === params.length - 1),
                'param-control-last-row': paramIndex === params.length - 1
            });
    const fillParamWithDefaultValue = <T extends TParam | TUniqueParam>
    (param: Partial<Param> & { name: string }, resetToDefault = false): T => {
        // prevent defaultsDeep mutate origin paramsDefaultValue
        const defaultParam = _.cloneDeep(deps.paramsDefaultValue[param.name]);
        if (defaultParam === undefined) throw Error(`Param ${param.name} not found in paramsDefaultValue`);
        defaultParam.subParam ??= {};
        if (!_.includes(_.keys(state.uniqueParams), param.name)) defaultParam.subParam.not = false; // add subParam.not on every param
        // cloneDeep to prevent defaultsDeep mutates origin object
        if (resetToDefault) return _.defaultsDeep(defaultParam, param);
        return _.defaultsDeep(_.cloneDeep(param), defaultParam);
    };
    const addParam = (selectDom: HTMLSelectElement) => {
        state.params.push(fillParamWithDefaultValue({ name: selectDom.value }));
        selectDom.value = 'add'; // reset to add option
    };
    const changeParam = (beforeParamIndex: number, afterParamName: string) => {
        _.pull(state.invalidParamsIndex, beforeParamIndex);
        state.params[beforeParamIndex] = fillParamWithDefaultValue({ name: afterParamName });
    };
    const deleteParam = (paramIndex: number) => {
        _.pull(state.invalidParamsIndex, paramIndex);
        // move forward index of all params, which after current
        state.invalidParamsIndex = state.invalidParamsIndex.map(invalidParamIndex =>
            (invalidParamIndex > paramIndex ? invalidParamIndex - 1 : invalidParamIndex));
        // eslint-disable-next-line @typescript-eslint/no-dynamic-delete
        state.params.splice(paramIndex, 1);
    };
    const clearParamDefaultValue = <T extends Param>(param: Param): Partial<Param | T> | null => {
        const defaultParam = _.cloneDeep(deps.paramsDefaultValue[param.name]);
        if (defaultParam === undefined) throw Error(`Param ${param.name} not found in paramsDefaultValue`);
        // remove subParam.not: false, which previously added by fillParamWithDefaultValue()
        if (defaultParam.subParam !== undefined) defaultParam.subParam.not ??= false;
        const newParam: Partial<Param> = _.cloneDeep(param); // prevent mutating origin param
        // number will consider as empty in isEmpty(), to prevent this we use complex short circuit evaluate expression
        if (!(_.isNumber(newParam.value) || !_.isEmpty(newParam.value))
            || (_.isArray(newParam.value) && _.isArray(defaultParam.value)
                ? _.isEqual(_.sortBy(newParam.value), _.sortBy(defaultParam.value)) // sort array type param value for comparing
                : newParam.value === defaultParam.value)) delete newParam.value;

        _.each(defaultParam.subParam, (value, _name) => {
            if (newParam.subParam === undefined) return;
            // undefined means this sub param must get deleted and merge into parent, as part of the parent param value
            // eslint-disable-next-line @typescript-eslint/no-dynamic-delete
            if (newParam.subParam[_name] === value || value === undefined) delete newParam.subParam[_name];
        });
        if (_.isEmpty(newParam.subParam)) delete newParam.subParam;

        return _.isEmpty(_.omit(newParam, 'name')) ? null : newParam; // return null for further filter()
    };
    const clearedParamsDefaultValue = (): Array<Partial<TParam>> =>
        _.filter(state.params.map(clearParamDefaultValue)) as Array<Partial<TParam>>; // filter() will remove falsy values like null
    const clearedUniqueParamsDefaultValue = (...omitParams: string[]): Partial<UniqueParams> =>
        // mapValues() return object which remains keys, pickBy() like filter() for objects
        _.pickBy(_.mapValues(_.omit(state.uniqueParams, omitParams), clearParamDefaultValue)) as Partial<UniqueParams>;
    const flattenParams = (): ObjUnknown[] => {
        const flattenParam = (param: Partial<Param>) => {
            const flatted: ObjUnknown = {};
            flatted[param.name ?? 'undef'] = param.value;
            return { ...flatted, ...param.subParam };
        };
        return [
            ...Object.values(clearedUniqueParamsDefaultValue()).map(flattenParam),
            ...clearedParamsDefaultValue().map(flattenParam)
        ];
    };
    const escapeParamValue = (value: string | unknown, shouldUnescape = false) => {
        let ret = value;
        if (_.isString(value)) {
            _.each({ // we don't escape ',' since array type params is already known
                '/': '%2F',
                ';': '%3B'
            }, (encode, char) => {
                ret = value.replace(shouldUnescape ? encode : char, shouldUnescape ? char : encode);
            });
        }
        return ret;
    };
    const parseParamRoute = (routePath: string[]) => {
        _.chain(routePath)
            .map(paramWithSub => {
                const parsedParam: ParamPartial = { name: '', subParam: {} };
                paramWithSub.split(';').forEach((params, paramIndex) => { // split multiple params
                    const paramPair: [string, unknown] = [
                        params.substr(0, params.indexOf(':')),
                        escapeParamValue(params.substr(params.indexOf(':') + 1), true)
                    ]; // split kv pair by first colon, using substr to prevent split array type param value
                    if (paramIndex === 0) { // main param
                        [parsedParam.name, parsedParam.value] = paramPair;
                    } else { // sub params
                        parsedParam.subParam = { ...parsedParam.subParam, [paramPair[0]]: paramPair[1] };
                    }
                });
                return parsedParam as Param;
            })
            .map(_.unary(fillParamWithDefaultValue))
            .each(param => {
                const preprocessor = deps.paramsPreprocessor[param.name];
                if (preprocessor !== undefined) {
                    preprocessor(param);
                    param.subParam.not = boolStrToBool(param.subParam.not);
                }
                const isUniqueParam = (p: Param): p is TUniqueParam => p.name in state.uniqueParams;
                if (isUniqueParam(param)) { // is unique param
                    state.uniqueParams[param.name as keyof UniqueParams] = param;
                } else {
                    state.params.push(param as TParam);
                }
            })
            .value();
    };
    const submitParamRoute = (filteredUniqueParams: Partial<UniqueParams>, filteredParams: Array<Partial<TParam>>) => {
        const paramValue = (v: unknown) => escapeParamValue(_.isArray(v) ? v.join(',') : v);
        const subParamValue = (subParam?: Param['subParam']) =>
            _.map(subParam, (value, _name) => `;${_name}:${escapeParamValue(value)}`).join('');
        void router.push({
            path: `/p/${[...Object.values(filteredUniqueParams), ...filteredParams]
                // format param to url, e.g. name:value;subParamName:subParamValue...
                .map(param => `${param.name}:${paramValue(param.value)}${subParamValue(param.subParam)}`)
                .join('/')}`
        });
    };

    watch([() => state.uniqueParams, () => state.params], newParamsArray => {
        const [uniqueParams, params] = newParamsArray;
        _.chain([...Object.values(uniqueParams), ...params])
            .filter(param => param.name in deps.paramsWatcher)
            .each(param => deps.paramsWatcher[param.name]?.(param))
            .value();
    }, { deep: true });
    onBeforeRouteUpdate((to, from) => {
        if (deps.parseRoute === undefined) throw Error('Unimplmented method in deps');
        if (to.path === from.path) return; // ignore when only hash has changed
        deps.parseRoute(to);
    });

    onBeforeMount(() => {
        state.uniqueParams = _.mapValues(state.uniqueParams, _.unary(fillParamWithDefaultValue)) as UniqueParams;
        state.params = state.params.map(_.unary(fillParamWithDefaultValue)) as typeof state.params;
        if (deps.parseRoute === undefined) throw Error('Unimplmented method in deps');
        deps.parseRoute(route); // first time parse
    });

    return {
        state,
        paramRowLastDomClass,
        addParam,
        changeParam,
        deleteParam,
        fillParamWithDefaultValue,
        clearParamDefaultValue,
        clearedParamsDefaultValue,
        clearedUniqueParamsDefaultValue,
        flattenParams,
        parseParamRoute,
        submitParamRoute
    };
};