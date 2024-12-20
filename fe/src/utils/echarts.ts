import { DateTime } from 'luxon';
import _ from 'lodash';
import * as echarts from 'echarts/core';
// eslint-disable-next-line import-x/extensions
import type { ColorPaletteOptionMixin } from 'echarts/types/src/util/types.d.ts';

export const useResizeableEcharts = (el: Parameters<typeof useResizeObserver>[0]) =>
    useResizeObserver(el, _.debounce((entries: readonly ResizeObserverEntry[]) => {
        entries.forEach(entry => {
            echarts.getInstanceByDom(entry.target as HTMLElement)?.resize();
        });
    }, 1000));

export const echarts4ColorTheme: ColorPaletteOptionMixin = {
    color: [
        '#c23531',
        '#2f4554',
        '#61a0a8',
        '#d48265',
        '#91c7ae',
        '#749f83',
        '#ca8622',
        '#bda29a',
        '#6e7074',
        '#546570',
        '#c4ccd3'
    ]
};

export const timeGranularities = ['minute', 'hour', 'day', 'week', 'month', 'year'] as const;
export type TimeGranularity = typeof timeGranularities[number];
export type TimeGranularityStringMap = Partial<Record<TimeGranularity, string>>;
export const timeGranularityAxisType: Record<TimeGranularity, 'category' | 'time'> = {
    ...keysWithSameValue(['minute', 'hour', 'day'], 'time'),
    ...keysWithSameValue(['week', 'month', 'year'], 'category')
};
export const timeGranularityAxisPointerLabelFormatter: (dateTimeTransformer: (dateTime: DateTime) => DateTime) =>
Record<TimeGranularity, (params: { value: Date | number | string }) => string> =
(dateTimeTransformer = i => i) => ({
    minute: ({ value }) => (_.isNumber(value)
        ? dateTimeTransformer(DateTime.fromMillis(value)).toLocaleString(DateTime.DATETIME_SHORT)
        : ''),
    hour: ({ value }) => (_.isNumber(value)
        ? dateTimeTransformer(DateTime.fromMillis(value))
            .toLocaleString({ year: 'numeric', month: 'long', day: 'numeric', hour: 'numeric' })
        : ''),
    day: ({ value }) => (_.isNumber(value)
        ? dateTimeTransformer(DateTime.fromMillis(value)).toLocaleString(DateTime.DATE_MED_WITH_WEEKDAY)
        : ''),
    week: ({ value }) => (_.isString(value) ? value : ''),
    month: ({ value }) => (_.isString(value) ? value : ''),
    year: ({ value }) => (_.isString(value) ? value : '')
});
