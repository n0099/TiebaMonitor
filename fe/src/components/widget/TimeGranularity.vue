<template>
<select v-model="modelValue" class="form-control">
    <option
        v-for="(text, granularity) in options" :key="granularity"
        :value="granularity" :selected="granularity === modelValue">
        {{ text }}
    </option>
</select>
</template>

<script setup lang="ts">
import _ from 'lodash';

defineOptions({ inheritAttrs: true });
const { granularities } = defineProps<{ granularities: string[] }>();
const modelValue = defineModel<string>();

const granularitiesDefaultOption: TimeGranularityStringMap = {
    minute: '分钟',
    hour: '小时',
    day: '天',
    week: '周',
    month: '月',
    year: '年'
};
const options = ref<TimeGranularityStringMap>(_.pick(granularitiesDefaultOption,
    _.intersection(granularities, Object.keys(granularitiesDefaultOption))));
</script>
