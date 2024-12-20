import _ from 'lodash';

// value of [0] will be either ALL: require exactly same post types, or SUB: requiring a subset of types
export type RequiredPostTypes = Record<string, ['ALL' | 'SUB', PostType[]] | undefined>;
export const requiredPostTypesKeyByParam: RequiredPostTypes = {
    pid: ['SUB', ['reply', 'subReply']],
    spid: ['ALL', ['subReply']],
    postContent: ['SUB', ['reply', 'subReply']],
    replySubReplyCount: ['ALL', ['reply']],
    authorExpGrade: ['SUB', ['reply', 'subReply']],
    ...keysWithSameValue([
        'latestReplyPostedAt',
        'threadTitle',
        'threadViewCount',
        'threadShareCount',
        'threadReplyCount',
        'threadProperties',
        'latestReplierUid',
        'latestReplierName',
        'latestReplierDisplayName',
        'latestReplierGender'
    ], ['ALL', ['thread']])
};
export const orderByRequiredPostTypes: RequiredPostTypes = {
    pid: ['SUB', ['reply', 'subReply']],
    spid: ['SUB', ['subReply']]
};

export const paramsNameKeyByType = {
    numeric: [
        'tid',
        'pid',
        'spid',
        'threadViewCount',
        'threadShareCount',
        'threadReplyCount',
        'replySubReplyCount',
        'authorUid',
        'authorExpGrade',
        'latestReplierUid'
    ],
    text: [
        'threadTitle',
        'postContent',
        'authorName',
        'authorDisplayName',
        'latestReplierName',
        'latestReplierDisplayName'
    ],
    dateTime: [
        'postedAt',
        'latestReplyPostedAt'
    ]
} as const;

export const numericParamSubParamRangeValues = ['<', '=', '>', 'BETWEEN', 'IN'] as const;
export interface NamelessParamNumeric {
    value: string,
    subParam: { range: typeof numericParamSubParamRangeValues[number] }
}
export const textParamSubParamMatchByValues = ['explicit', 'implicit', 'regex'] as const;
export interface NamelessParamText {
    value: string,
    subParam: {
        matchBy: typeof textParamSubParamMatchByValues[number],
        spaceSplit: boolean
    }
}
interface NamelessParamDateTime { value: string, subParam: { range: undefined } }
interface NamelessParamGender { value: '0' | '1' | '2' }
interface NamelessParamsOther {
    threadProperties: { value: Array<'good' | 'sticky'> },
    authorManagerType: { value: ForumModeratorType | 'NULL' }
}

export type AddNameToParam<Name, NamelessParam> = NamelessParam & { name: Name, value: unknown, subParam: ObjEmpty };
export type KnownParams = { [P in 'authorGender' | 'latestReplierGender']: AddNameToParam<P, NamelessParamGender> }
& { [P in keyof NamelessParamsOther]: AddNameToParam<P, NamelessParamsOther[P]> }
& { [P in typeof paramsNameKeyByType.dateTime[number]]: AddNameToParam<P, NamelessParamDateTime> }
& { [P in typeof paramsNameKeyByType.numeric[number]]: AddNameToParam<P, NamelessParamNumeric> }
& { [P in typeof paramsNameKeyByType.text[number]]: AddNameToParam<P, NamelessParamText> };
export type KnownDateTimeParams = KnownParams[typeof paramsNameKeyByType.dateTime[number]];
export type KnownNumericParams = KnownParams[typeof paramsNameKeyByType.numeric[number]];
export type KnownTextParams = KnownParams[typeof paramsNameKeyByType.text[number]];
export interface KnownUniqueParams extends Record<string, UnknownParam> {
    fid: { name: 'fid', value: Fid, subParam: ObjEmpty },
    postTypes: { name: 'postTypes', value: PostType[], subParam: ObjEmpty },
    orderBy: {
        name: 'orderBy',
        value: PostIDStr | 'default' | 'postedAt',
        subParam: { direction: 'ASC' | 'default' | 'DESC' }
    }
}

const paramsMetadataKeyByType: Record<'array' | 'dateTimeRange' | 'numeric' | 'textMatch', {
    default?: NamelessUnknownParam,
    preprocessor?: ParamPreprocessorOrWatcher,
    watcher?: ParamPreprocessorOrWatcher
}> = { // mutating param object will sync changes
    array: {
        preprocessor(param) {
            if (_.isString(param.value))
                param.value = param.value.split(',');
        }
    },
    numeric: { default: { subParam: { range: '=' } } },
    textMatch: {
        default: { subParam: { matchBy: 'explicit', spaceSplit: false } },
        preprocessor(param) {
            param.subParam.spaceSplit = boolStrToBool(param.subParam.spaceSplit);
        },
        watcher(param) {
            if (param.subParam.matchBy === 'regex')
                param.subParam.spaceSplit = false;
        }
    },
    dateTimeRange: {
        default: { subParam: { range: undefined } },
        preprocessor(param) {
            if (!_.isString(param.value))
                return;
            param.subParam.range = param.value.split(',');
        },
        watcher(param) {
            // combine datetime range into root param's value
            param.value = _.isArray(param.subParam.range) ? param.subParam.range.join(',') : '';
        }
    }
};
const paramsDefaultValue = {
    fid: { value: 0, subParam: {} },
    postTypes: { value: postType, subParam: {} },
    orderBy: { value: 'default', subParam: { direction: 'default' } },
    threadProperties: { value: [] },
    authorManagerType: { value: 'NULL' },
    authorGender: { value: '0' },
    latestReplierGender: { value: '0' },
    ..._.mapValues(_.keyBy(paramsNameKeyByType.numeric), () =>
        paramsMetadataKeyByType.numeric.default),
    ..._.mapValues(_.keyBy(paramsNameKeyByType.text), () =>
        paramsMetadataKeyByType.textMatch.default),
    ..._.mapValues(_.keyBy(paramsNameKeyByType.dateTime), () =>
        paramsMetadataKeyByType.dateTimeRange.default)
} as const;
const useQueryFormDependency: Parameters<typeof useQueryForm>[0] = {
    paramsDefaultValue,
    paramsPreprocessor: {
        postTypes: paramsMetadataKeyByType.array.preprocessor,
        threadProperties: paramsMetadataKeyByType.array.preprocessor,
        ..._.mapValues(_.keyBy(paramsNameKeyByType.text), () =>
            paramsMetadataKeyByType.textMatch.preprocessor),
        ..._.mapValues(_.keyBy(paramsNameKeyByType.dateTime), () =>
            paramsMetadataKeyByType.dateTimeRange.preprocessor)
    },
    paramsWatcher: {
        ..._.mapValues(_.keyBy(paramsNameKeyByType.text), () =>
            paramsMetadataKeyByType.textMatch.watcher),
        ..._.mapValues(_.keyBy(paramsNameKeyByType.dateTime), () =>
            paramsMetadataKeyByType.dateTimeRange.watcher),
        orderBy(param) {
            if (param.value === 'default' && param.subParam.direction !== 'default') { // reset to default
                param.subParam = { ...param.subParam, direction: 'default' };
            }
        }
    }
};

/** must get invoked with in the {@link setup()} of component */
export const useQueryFormWithUniqueParams = () => {
    const ret = useQueryForm<KnownUniqueParams, KnownParams>(useQueryFormDependency);
    ret.uniqueParams.value = {
        fid: { name: 'fid', ...paramsDefaultValue.fid },
        postTypes: {
            name: 'postTypes',
            ...paramsDefaultValue.postTypes as DeepWritable<typeof paramsDefaultValue.postTypes>
        },
        orderBy: { name: 'orderBy', ...paramsDefaultValue.orderBy }
    };

    return ret;
};
