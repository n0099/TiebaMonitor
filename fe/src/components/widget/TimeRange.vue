<template>
<ARangePicker
    v-model="timeRange" :ranges="{
        昨天: [
            dayjs().subtract(1, 'day').startOf('day'),
            dayjs().subtract(1, 'day').endOf('day')
        ],
        今天: [dayjs().startOf('day'), dayjs().endOf('day')],
        本周: [dayjs().startOf('week'), dayjs().endOf('week')],
        最近7天: [dayjs().subtract(7, 'days'), dayjs()],
        本月: [dayjs().startOf('month'), dayjs().endOf('month')],
        最近30天: [dayjs().subtract(30, 'days'), dayjs()]
    }" format="YYYY-MM-DD HH:mm" :showTime="{
        format: 'HH:mm',
        minuteStep: 5,
        secondStep: 10
    }" :allowClear="false" size="large" class="col" />
</template>

<script setup lang="ts">
import type { Dayjs } from 'dayjs';
import dayjs, { unix } from 'dayjs';
import type { DurationLike } from 'luxon';
import { DateTime } from 'luxon';

defineOptions({ inheritAttrs: true });
const { startTime = 0, endTime = 0, startBefore } = defineProps<{
    startTime?: number,
    endTime?: number,
    startBefore: DurationLike
}>();
const emit = defineEmits<{
    'update:startTime': [i: number],
    'update:endTime': [i: number]
}>();

const timeRange = ref<[Dayjs, Dayjs]>((now => [
    dayjs(now.minus(startBefore).startOf('minute').toISO()),
    dayjs(now.startOf('minute').toISO())
])(DateTime.now()));

watchEffect(() => {
    timeRange.value = [unix(startTime), unix(endTime)];
});
watchEffect(() => {
    emit('update:startTime', timeRange.value[0].unix());
    emit('update:endTime', timeRange.value[1].unix());
});
</script>
